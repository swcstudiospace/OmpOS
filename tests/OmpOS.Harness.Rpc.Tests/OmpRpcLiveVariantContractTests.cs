using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Rpc.Tests;

/// <summary>
/// Replays the top-level RPC frame types of the live <c>omp --mode rpc</c> capture of 2026-10-21 (10 turns). Every type
/// absent from the committed <c>rpc-out.ndjson</c> is asserted either to map to a harness event or to stay on the mapper's
/// unknown fallback (<c>null</c>, which the session drops without a diagnostic). The capture is read from the source tree,
/// because the test project copies only <c>*.ndjson</c> to its output.
/// </summary>
public sealed class OmpRpcLiveVariantContractTests
{
    private const string CaptureName = "rpc-live-20261021T171351Z.ndjson";
    private const string SidecarName = "rpc-live-20261021T171351Z.turns.json";
    private const string MessageUpdate = "message_update";
    private const string PromptResult = "prompt_result";
    private const string Completed = "completed";
    private const string Prompt = "Reply with the single word ok.";

    /// <summary><c>agent_start</c> is a lifecycle marker with no content and reaches the fallback.</summary>
    [Fact]
    public void AgentStartReachesFallback() => AssertUnmodelled("agent_start", 10);

    /// <summary><c>turn_start</c> is a lifecycle marker with no content and reaches the fallback.</summary>
    [Fact]
    public void TurnStartReachesFallback() => AssertUnmodelled("turn_start", 10);

    /// <summary>
    /// <c>turn_end</c> carries one assistant message and is not terminal: a prompt can span several turns, and the stream
    /// completes on <c>prompt_result</c>. It reaches the fallback.
    /// </summary>
    [Fact]
    public void TurnEndReachesFallback() => AssertUnmodelled("turn_end", 10);

    /// <summary><c>agent_end</c> carries the whole message history and is not terminal, so it reaches the fallback.</summary>
    [Fact]
    public void AgentEndReachesFallback() => AssertUnmodelled("agent_end", 10);

    /// <summary><c>message_start</c> brackets one message with no delta content and reaches the fallback.</summary>
    [Fact]
    public void MessageStartReachesFallback() => AssertUnmodelled("message_start", 30);

    /// <summary><c>message_end</c> brackets one message with no delta content and reaches the fallback.</summary>
    [Fact]
    public void MessageEndReachesFallback() => AssertUnmodelled("message_end", 30);

    /// <summary><c>session_settled</c> is a post-turn settle signal with no content and reaches the fallback.</summary>
    [Fact]
    public void SessionSettledReachesFallback() => AssertUnmodelled("session_settled", 10);

    /// <summary><c>thinking_level_changed</c> is a configuration change, not a diagnostic, and reaches the fallback.</summary>
    [Fact]
    public void ThinkingLevelChangedReachesFallback() => AssertUnmodelled("thinking_level_changed", 10);

    /// <summary>
    /// <c>available_commands_update</c> is emitted on the RPC transport (3 frames in this capture). The mapper has no
    /// command-list event, so it reaches the fallback.
    /// </summary>
    [Fact]
    public void AvailableCommandsUpdateReachesFallback() => AssertUnmodelled("available_commands_update", 3);

    /// <summary>
    /// <c>message_update</c> maps <c>text_delta</c> to <see cref="TextDeltaEvent"/> and <c>thinking_delta</c> to
    /// <see cref="ThoughtEvent"/>. Its block markers (<c>text_start</c>, <c>text_end</c>, <c>thinking_start</c>,
    /// <c>thinking_end</c>) carry no delta and reach the fallback.
    /// </summary>
    [Fact]
    public void MessageUpdateMapsDeltasAndFallsBackOnBlockMarkers()
    {
        List<JsonElement> frames = FramesOf(MessageUpdate);
        List<HarnessEvent?> events = frames.Select(frame => RpcEventMapper.Map(MessageUpdate, frame)).ToList();

        frames.Count.ShouldBe(127);
        events.OfType<TextDeltaEvent>().Count().ShouldBe(10);
        events.OfType<TextDeltaEvent>().Select(delta => delta.Text).Distinct(StringComparer.Ordinal).ShouldBe(["ok"]);
        events.OfType<ThoughtEvent>().Count().ShouldBe(83);
        events.Count(mapped => mapped is null).ShouldBe(34);
        frames
            .Where(frame => RpcEventMapper.Map(MessageUpdate, frame) is null)
            .Select(frame => frame.GetProperty("assistantMessageEvent").GetProperty("type").GetString())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ShouldBe(["text_end", "text_start", "thinking_end", "thinking_start"]);
    }

    /// <summary>
    /// <c>prompt_result</c> is not mapped by <see cref="RpcEventMapper"/>; the session completes the prompt on it. Replayed
    /// through a session, the first live turn's stream yields its text delta and one <see cref="CompletedEvent"/> with the
    /// <c>completed</c> reason, as the last event. The capture's prompt acknowledgement carries no <c>data</c> member, so
    /// the replay supplies <c>agentInvoked: true</c> for that acknowledgement and keeps every turn frame verbatim.
    /// </summary>
    [Fact]
    public async Task PromptResultCompletesTheTurnThroughTheSession()
    {
        var ct = TestContext.Current.CancellationToken;
        List<string> lines = CaptureLines();
        List<string> turn = FirstTurn(lines);
        var transport = new FakeRpcTransport(command => Respond(command, lines, turn));
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        try
        {
            transport.Emit(lines.First(line => string.Equals(FrameType(line), "ready", StringComparison.Ordinal)));

            await session.CreateAsync(ct).ConfigureAwait(true);
            List<HarnessEvent> events = await CollectAsync(session.SubmitPromptAsync(Prompt, new PromptOptions(), ct)).ConfigureAwait(true);

            events.OfType<TextDeltaEvent>().Select(delta => delta.Text).ShouldBe(["ok"]);
            events.OfType<CompletedEvent>().ShouldBe([new CompletedEvent(Completed)]);
            events.Last().ShouldBe(new CompletedEvent(Completed));
            session.State.ShouldBe(HarnessState.Ready);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
            await transport.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Every live turn in the sidecar has a non-empty stop reason, and all 10 turns completed.</summary>
    [Fact]
    public void EveryLiveTurnHasANonEmptyStopReasonAndAllTurnsCompleted()
    {
        using JsonDocument sidecar = JsonDocument.Parse(File.ReadAllText(Path.Combine(FramesDirectory(), SidecarName)));
        IReadOnlyList<(int Turn, string? StopReason)> turns = sidecar.RootElement.EnumerateArray()
            .Select(turn => (turn.GetProperty("turn").GetInt32(), turn.GetProperty("stopReason").GetString()))
            .ToList();

        turns.Select(turn => turn.Turn).ShouldBe(Enumerable.Range(1, 10));
        turns.Count(turn => string.IsNullOrWhiteSpace(turn.StopReason)).ShouldBe(0);
        turns.Count(turn => !string.Equals(turn.StopReason, Completed, StringComparison.Ordinal)).ShouldBe(0);
    }

    private static void AssertUnmodelled(string type, int expectedCount)
    {
        List<JsonElement> frames = FramesOf(type);
        frames.Count.ShouldBe(expectedCount);
        frames.Count(frame => RpcEventMapper.Map(type, frame) is not null).ShouldBe(0);
    }

    private static List<JsonElement> FramesOf(string type)
    {
        var frames = new List<JsonElement>();
        foreach (string line in CaptureLines())
        {
            using JsonDocument document = JsonDocument.Parse(line);
            if (string.Equals(OmpRpcFrames.ReadType(document.RootElement), type, StringComparison.Ordinal))
            {
                frames.Add(document.RootElement.Clone());
            }
        }

        return frames;
    }

    private static List<string> FirstTurn(List<string> lines)
    {
        int start = lines.FindIndex(line => string.Equals(FrameType(line), "agent_start", StringComparison.Ordinal));
        int end = lines.FindIndex(line => string.Equals(FrameType(line), PromptResult, StringComparison.Ordinal));
        return lines.GetRange(start, end - start + 1)
            .Where(line => FrameType(line) is not ("response" or "ready"))
            .ToList();
    }

    private static IEnumerable<string> Respond(string command, List<string> lines, List<string> turn)
    {
        string type = RpcTestData.TypeOf(command);
        string id = RpcTestData.IdOf(command);
        switch (type)
        {
            case "negotiate_protocol":
            case "get_state":
                return [RpcTestData.WithId(FirstResponse(lines, type), id)];
            case "prompt":
                return [RpcTestData.Ok(id, type, """{"agentInvoked":true}"""), .. turn.Select(line => RewritePromptResult(line, id))];
            default:
                return [];
        }
    }

    private static string RewritePromptResult(string line, string id) =>
        string.Equals(FrameType(line), PromptResult, StringComparison.Ordinal) ? RpcTestData.WithId(line, id) : line;

    private static string FirstResponse(List<string> lines, string command) =>
        lines.First(line => string.Equals(FrameType(line), OmpRpcFrames.ResponseType, StringComparison.Ordinal)
            && string.Equals(JsonNode.Parse(line)!["command"]?.GetValue<string>(), command, StringComparison.Ordinal));

    private static string? FrameType(string line)
    {
        using JsonDocument document = JsonDocument.Parse(line);
        return OmpRpcFrames.ReadType(document.RootElement);
    }

    private static List<string> CaptureLines() =>
        File.ReadAllLines(Path.Combine(FramesDirectory(), CaptureName)).ToList();

    private static string FramesDirectory() =>
        Path.Combine(RepoRoot(), "tests", "OmpOS.Harness.Rpc.Tests", "TestData", "frames");

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OmpOS.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("OmpOS.slnx was not found above the test output directory.");
    }

    private static async Task<List<HarnessEvent>> CollectAsync(IAsyncEnumerable<HarnessEvent> events)
    {
        var collected = new List<HarnessEvent>();
        IAsyncEnumerator<HarnessEvent> enumerator = events.GetAsyncEnumerator();
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(true))
            {
                collected.Add(enumerator.Current);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(true);
        }

        return collected;
    }
}
