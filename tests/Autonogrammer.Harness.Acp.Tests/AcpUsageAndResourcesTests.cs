using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>The usage update becomes a context usage event, and mention resources travel as embedded resource blocks of the prompt.</summary>
public sealed class AcpUsageAndResourcesTests
{
    private static byte[] Update(string update) =>
        AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{{\"sessionId\":\"{AcpFixtures.CapturedSessionId}\",\"update\":{update}}}}}");

    private static IEnumerable<byte[]> Script(byte[] frame, JsonObject[] seen)
    {
        if (!string.Equals(AcpFixtures.MethodOf(frame), "session/prompt", StringComparison.Ordinal))
        {
            return AcpFixtures.Reply(frame);
        }

        seen[0] = JsonNode.Parse(frame)!.AsObject();
        return
        [
            Update("{\"sessionUpdate\":\"usage_update\",\"used\":1200,\"size\":200000}"),
            AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"result\":{{\"stopReason\":\"end_turn\"}}}}"),
        ];
    }

    private static async Task<List<HarnessEvent>> RunAsync(PromptOptions options, JsonObject[] seen, CancellationToken ct)
    {
        var transport = new ScriptedAcpTransport(frame => Script(frame, seen));
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" });
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var events = new List<HarnessEvent>();
        await foreach (var harnessEvent in session.SubmitPromptAsync("look at this", options, ct).WithCancellation(ct).ConfigureAwait(true))
        {
            events.Add(harnessEvent);
        }

        return events;
    }

    [Fact]
    public async Task AUsageUpdateIsAContextUsageEventBeforeTheCompletion()
    {
        var events = await RunAsync(new PromptOptions(), new JsonObject[1], TestContext.Current.CancellationToken).ConfigureAwait(true);

        events[0].ShouldBe(new ContextUsageEvent(1200, 200000));
        events[1].ShouldBe(new CompletedEvent("end_turn"));
    }

    /// <summary>A usage update that carries a cost keeps the amount and currency; one without a cost stays equal to the two-argument event.</summary>
    [Fact]
    public async Task AUsageUpdateWithCostKeepsTheAmountAndOneWithoutStaysNull()
    {
        var withCost = await RunScriptAsync(Update("{\"sessionUpdate\":\"usage_update\",\"used\":10,\"size\":20,\"cost\":{\"amount\":1.25,\"currency\":\"USD\"}}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        withCost[0].ShouldBe(new ContextUsageEvent(10, 20, new UsageCost(1.25m, "USD")));

        var without = await RunAsync(new PromptOptions(), new JsonObject[1], TestContext.Current.CancellationToken).ConfigureAwait(true);
        without[0].ShouldBe(new ContextUsageEvent(1200, 200000));
        (without[0] as ContextUsageEvent)!.Cost.ShouldBeNull();
    }

    private static async Task<List<HarnessEvent>> RunScriptAsync(byte[] usageFrame, CancellationToken ct)
    {
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (!string.Equals(AcpFixtures.MethodOf(frame), "session/prompt", StringComparison.Ordinal))
            {
                return AcpFixtures.Reply(frame);
            }

            return
            [
                usageFrame,
                AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"result\":{{\"stopReason\":\"end_turn\"}}}}"),
            ];
        });
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" });
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var events = new List<HarnessEvent>();
        await foreach (var harnessEvent in session.SubmitPromptAsync("look at this", new PromptOptions(), ct).WithCancellation(ct).ConfigureAwait(true))
        {
            events.Add(harnessEvent);
        }

        return events;
    }

    [Fact]
    public async Task APromptWithoutResourcesIsOneTextBlock()
    {
        var seen = new JsonObject[1];
        _ = await RunAsync(new PromptOptions(), seen, TestContext.Current.CancellationToken).ConfigureAwait(true);

        var blocks = seen[0]["params"]!["prompt"]!.AsArray();
        blocks.Count.ShouldBe(1);
        blocks[0]!["type"]!.GetValue<string>().ShouldBe("text");
    }

    [Fact]
    public async Task AResourceIsSentEmbeddedWithItsTextAndTheUserTextIsKept()
    {
        var seen = new JsonObject[1];
        var options = new PromptOptions { Resources = [new PromptResource(new Uri("file:///work/a.txt"), "text/plain", "contents of a", "a.txt", 13, false)] };

        _ = await RunAsync(options, seen, TestContext.Current.CancellationToken).ConfigureAwait(true);

        var text = seen[0].ToJsonString();
        text.ShouldContain("\"resource\"", Case.Sensitive);
        text.ShouldContain("contents of a", Case.Sensitive);
        text.ShouldContain("file:///work/a.txt", Case.Sensitive);
        text.ShouldContain("look at this", Case.Sensitive);
    }

    private const string StandingUri = "autonogrammer://cowork/standing-context/task-1";
    private const string StandingText = "global-canary é 🙂\n\n<untrusted>project-canary</untrusted>";

    private static PromptResource Standing() => new(new Uri(StandingUri), "text/plain", StandingText, "Task context", System.Text.Encoding.UTF8.GetByteCount(StandingText), false);

    // The handshake, a new session, a load and a prompt. A load replays the given frames before it answers. Every frame the client sent is kept in seen.
    private static IEnumerable<byte[]> Peer(byte[] frame, bool embeddedContext, List<JsonObject> seen, IReadOnlyList<byte[]>? replay = null)
    {
        seen.Add(JsonNode.Parse(frame)!.AsObject());
        var id = AcpFixtures.IdOf(frame);
        return AcpFixtures.MethodOf(frame) switch
        {
            "initialize" => [Answer(id, "{\"protocolVersion\":1,\"agentCapabilities\":{\"loadSession\":true,\"promptCapabilities\":{\"embeddedContext\":" + (embeddedContext ? "true" : "false") + ",\"image\":false}}}")],
            "session/new" => [Answer(id, "{\"sessionId\":\"sess-peer\"}")],
            "session/load" => [.. replay ?? Array.Empty<byte[]>(), Answer(id, "{}")],
            "session/prompt" => [Answer(id, "{\"stopReason\":\"end_turn\"}")],
            _ => [],
        };
    }

    private static byte[] Answer(long id, string resultJson) => AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{resultJson}}}");

    private static byte[] Replayed(string sessionId, JsonObject update) => AcpFixtures.Utf8(new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["method"] = "session/update",
        ["params"] = new JsonObject { ["sessionId"] = sessionId, ["update"] = update },
    }.ToJsonString());

    private static JsonObject UserBlock(string uri, string text) => new()
    {
        ["sessionUpdate"] = "user_message_chunk",
        ["content"] = new JsonObject
        {
            ["type"] = "resource",
            ["resource"] = new JsonObject { ["uri"] = uri, ["mimeType"] = "text/plain", ["text"] = text },
        },
    };

    private static JsonObject AgentChunk(string text) => new()
    {
        ["sessionUpdate"] = "agent_message_chunk",
        ["content"] = new JsonObject { ["type"] = "text", ["text"] = text },
    };

    private static List<JsonObject> PromptFrames(List<JsonObject> seen) =>
        [.. seen.Where(static frame => string.Equals(frame["method"]?.GetValue<string>(), "session/prompt", StringComparison.Ordinal))];

    private static async Task<List<HarnessEvent>> DrainAsync(IAsyncEnumerable<HarnessEvent> events, CancellationToken cancellationToken)
    {
        var drained = new List<HarnessEvent>();
        await foreach (var harnessEvent in events.WithCancellation(cancellationToken).ConfigureAwait(true))
        {
            drained.Add(harnessEvent);
        }

        return drained;
    }

    /// <summary>A required embedded block is refused by the session before anything is written: no prompt frame leaves and transmission is not reported.</summary>
    [Fact]
    public async Task ARequiredEmbeddedContextIsRefusedBeforeAnythingIsSent()
    {
        var ct = TestContext.Current.CancellationToken;
        var seen = new List<JsonObject>();
        var transport = new ScriptedAcpTransport(frame => Peer(frame, embeddedContext: false, seen));
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd });
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var transmissions = 0;
        var options = new PromptOptions { Resources = [Standing()], RequireEmbeddedContext = true, Transmitted = () => transmissions++ };

        var refusal = await Should.ThrowAsync<NotSupportedException>(() => DrainAsync(session.SubmitPromptAsync("look", options, ct), ct)).ConfigureAwait(true);

        refusal.Message.ShouldContain("embeddedContext", Case.Sensitive);
        transmissions.ShouldBe(0);
        PromptFrames(seen).Count.ShouldBe(0);
    }

    /// <summary>The standing block is the first embedded block, byte-exact, and the user text follows; the transmission is reported once the request was written.</summary>
    [Fact]
    public async Task AStandingBlockIsSentFirstByteExactAndTransmissionIsReportedOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var seen = new List<JsonObject>();
        var transport = new ScriptedAcpTransport(frame => Peer(frame, embeddedContext: true, seen));
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd });
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var transmissions = 0;
        var options = new PromptOptions { Resources = [Standing()], RequireEmbeddedContext = true, Transmitted = () => transmissions++ };

        var events = await DrainAsync(session.SubmitPromptAsync("look at this", options, ct), ct).ConfigureAwait(true);

        events.OfType<CompletedEvent>().Count().ShouldBe(1);
        transmissions.ShouldBe(1);
        var blocks = PromptFrames(seen).Single()["params"]!["prompt"]!.AsArray();
        blocks.Count.ShouldBe(2);
        blocks[0]!["type"]!.GetValue<string>().ShouldBe("resource");
        blocks[0]!["resource"]!["uri"]!.GetValue<string>().ShouldBe(StandingUri);
        blocks[0]!["resource"]!["text"]!.GetValue<string>().ShouldBe(StandingText);
        blocks[1]!["type"]!.GetValue<string>().ShouldBe("text");
        blocks[1]!["text"]!.GetValue<string>().ShouldBe("look at this");
    }

    /// <summary>An ordinary mention still travels when the agent lacks embedded context and the caller did not require it: Code behaviour is unchanged.</summary>
    [Fact]
    public async Task AnOrdinaryMentionIsUnchangedWhenEmbeddedContextIsNotRequired()
    {
        var ct = TestContext.Current.CancellationToken;
        var seen = new List<JsonObject>();
        var transport = new ScriptedAcpTransport(frame => Peer(frame, embeddedContext: false, seen));
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd });
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var options = new PromptOptions { Resources = [Standing()] };

        _ = await DrainAsync(session.SubmitPromptAsync("look", options, ct), ct).ConfigureAwait(true);

        var blocks = PromptFrames(seen).Single()["params"]!["prompt"]!.AsArray();
        blocks[0]!["type"]!.GetValue<string>().ShouldBe("resource");
        blocks[^1]!["text"]!.GetValue<string>().ShouldBe("look");
    }

    /// <summary>The replay a session/load sends reaches the load's sink with the replayed bytes, in wire order, and is complete.</summary>
    [Fact]
    public async Task ALoadReplayReachesTheSinkWithItsBytesAndIsComplete()
    {
        var ct = TestContext.Current.CancellationToken;
        var seen = new List<JsonObject>();
        var sink = new RecordingReplaySink();
        byte[][] replay = [Replayed("sess-resume", UserBlock(StandingUri, StandingText)), Replayed("sess-resume", AgentChunk("reply"))];
        var transport = new ScriptedAcpTransport(frame => Peer(frame, embeddedContext: true, seen, replay));
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, LoadReplaySink = sink });
        await using var sessionScope = session.ConfigureAwait(true);

        await session.ResumeAsync("sess-resume", ct).ConfigureAwait(true);

        sink.Replays.Count.ShouldBe(1);
        var captured = sink.Replays[0];
        captured.SessionId.ShouldBe("sess-resume");
        captured.Complete.ShouldBeTrue();
        captured.Updates.Count.ShouldBe(2);
        captured.Updates[0].GetProperty("content").GetProperty("resource").GetProperty("text").GetString().ShouldBe(StandingText);
    }

    /// <summary>A replay longer than the session keeps is cut at the cap and marked incomplete, never presented as the whole history.</summary>
    [Fact]
    public async Task AReplayPastTheCapIsMarkedIncomplete()
    {
        var ct = TestContext.Current.CancellationToken;
        var seen = new List<JsonObject>();
        var sink = new RecordingReplaySink();
        var replay = Enumerable.Range(0, 8193).Select(index => Replayed("sess-long", AgentChunk("chunk " + index.ToString(System.Globalization.CultureInfo.InvariantCulture)))).ToArray();
        var transport = new ScriptedAcpTransport(frame => Peer(frame, embeddedContext: true, seen, replay));
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, LoadReplaySink = sink });
        await using var sessionScope = session.ConfigureAwait(true);

        await session.ResumeAsync("sess-long", ct).ConfigureAwait(true);

        sink.Replays.Count.ShouldBe(1);
        sink.Replays[0].Updates.Count.ShouldBe(8192);
        sink.Replays[0].Complete.ShouldBeFalse();
    }

    private sealed class RecordingReplaySink : IAcpLoadReplaySink
    {
        public List<AcpLoadReplay> Replays { get; } = [];

        public void LoadReplayed(AcpLoadReplay replay) => Replays.Add(replay);
    }
}
