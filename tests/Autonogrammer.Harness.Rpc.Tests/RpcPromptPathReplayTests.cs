using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Rpc.Tests;

/// <summary>
/// Phase 80 Task 1: the single-session prompt path proven by replay only (OD-07 — no live model turn, no omp
/// process is spawned). Handshake frames are the frozen 19.1.1 capture
/// (<c>rpc-observed-19.1.1-20261123T202249Z.ndjson</c>, ledger R-READY/R-NEGOTIATE/R-CAPS); prompt-path
/// frames are extracted verbatim from the verified-18.12.6 live capture
/// (<c>rpc-live-20261021T171351Z.ndjson</c>, ledger command table <c>prompt</c>/<c>abort</c> rows:
/// "verified-18.12.6 shape stands"). Frame kinds the mapper does not model (for example
/// <c>agent_start</c>, <c>turn_start</c>, <c>message_start</c>, <c>session_settled</c> and
/// <c>thinking_level_changed</c>, all present in the live capture) stay dropped by design and are pinned
/// that way here; no frame kind is invented. The live capture contains no <c>tool_execution_*</c> frames,
/// so none are scripted here; tool mapping stays covered by <see cref="RpcHarnessSessionFactoryTests"/>.
/// </summary>
public sealed class RpcPromptPathReplayTests
{
    /// <summary>
    /// One replayed session: 19.1.1 handshake, then a prompt whose streamed
    /// thinking/text deltas and terminal are the live capture's own lines, mapping to the exact
    /// <see cref="HarnessEvent"/> sequence.
    /// </summary>
    [Fact]
    public async Task PromptToCompletion_ReplaysLiveCaptureDeltasAndTerminal()
    {
        var ct = TestContext.Current.CancellationToken;
        // Live capture seq 17 is the first thinking_delta ("This") and seq 38 the first text_delta ("ok");
        // both lines are replayed verbatim after the prompt admission (seq 5 shape, re-keyed).
        string thinkingDelta = LiveDelta("thinking_delta");
        string textDelta = LiveDelta("text_delta");

        await RunAsync(
            (command, id) => command switch
            {
                "prompt" =>
                [
                    PromptAdmission(id),
                    thinkingDelta,
                    textDelta,
                    LivePromptResult(id),
                ],
                _ => null,
            },
            async session =>
            {
                session.AdvertisedCapabilities["model.id"].ShouldBe("claude-opus-4-5");
                session.AdvertisedCapabilities["model.provider"].ShouldBe("anthropic");

                List<HarnessEvent> events = await CollectAsync(session.SubmitPromptAsync("reply ok", new PromptOptions(), ct)).ConfigureAwait(true);

                events.ShouldBe([new ThoughtEvent("This"), new TextDeltaEvent("ok"), new CompletedEvent("completed")]);
                session.State.ShouldBe(HarnessState.Ready);
            },
            ct).ConfigureAwait(true);
    }

    /// <summary>
    /// Cancelling the running replayed prompt sends <c>abort</c> (ledger command table) and the
    /// <c>prompt_result</c> with <c>status: aborted</c> (the terminal vocabulary <see cref="PromptResultFrame"/>
    /// codes against) settles the stream to the cancelled reason.
    /// </summary>
    [Fact]
    public async Task Abort_SettlesRunningPromptToCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        string? promptId = null;
        await RunAsync(
            (command, id) =>
            {
                switch (command)
                {
                    case "prompt":
                        promptId = id;
                        return [PromptAdmission(id)];
                    case "abort":
                        return [RpcTestData.Ok(id, "abort", "null"), RpcTestData.PromptResult(promptId!, "aborted")];
                    default:
                        return null;
                }
            },
            async session =>
            {
                Task<List<HarnessEvent>> consumer = CollectAsync(session.SubmitPromptAsync("long task", new PromptOptions(), ct));
                await session.CancelAsync(ct).ConfigureAwait(true);
                List<HarnessEvent> events = await consumer.ConfigureAwait(true);

                events.ShouldBe([new CompletedEvent("cancelled")]);
                session.State.ShouldBe(HarnessState.Ready);
            },
            ct).ConfigureAwait(true);
    }

    /// <summary>
    /// A prompt answered <c>agentInvoked: false</c> (a local command the engine handled itself; the admission
    /// shape <see cref="PromptResultFrame.AgentInvoked"/> documents) completes inline with no
    /// <c>prompt_result</c> following.
    /// </summary>
    [Fact]
    public async Task AgentNotInvoked_CompletesInlineWithoutPromptResult()
    {
        var ct = TestContext.Current.CancellationToken;
        await RunAsync(
            (command, id) => command switch
            {
                "prompt" => [RpcTestData.Ok(id, "prompt", """{"agentInvoked":false}""")],
                _ => null,
            },
            async session =>
            {
                List<HarnessEvent> events = await CollectAsync(session.SubmitPromptAsync("/help", new PromptOptions(), ct)).ConfigureAwait(true);

                events.ShouldBe([new CompletedEvent("completed")]);
                session.State.ShouldBe(HarnessState.Ready);
            },
            ct).ConfigureAwait(true);
    }

    /// <summary>
    /// Unmapped live-capture frame kinds around the turn (<c>agent_start</c> seq 9, <c>turn_start</c> seq 10,
    /// <c>message_start</c> seq 11) are dropped by <see cref="RpcEventMapper"/>: the consumer sees only the
    /// terminal. No new mapping is invented for them in this phase.
    /// </summary>
    [Fact]
    public async Task UnmappedAgentTurnMessageFrames_AreDroppedNotInvented()
    {
        var ct = TestContext.Current.CancellationToken;
        IReadOnlyList<string> live = RpcTestData.LiveCapture();
        await RunAsync(
            (command, id) => command switch
            {
                "prompt" =>
                [
                    PromptAdmission(id),
                    LiveFrame(live, "agent_start"),
                    LiveFrame(live, "turn_start"),
                    LiveFrame(live, "message_start"),
                    LivePromptResult(id),
                ],
                _ => null,
            },
            async session =>
            {
                List<HarnessEvent> events = await CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct)).ConfigureAwait(true);

                events.ShouldBe([new CompletedEvent("completed")]);
                session.State.ShouldBe(HarnessState.Ready);
            },
            ct).ConfigureAwait(true);
    }

    /// <summary>
    /// Builds the scripted peer: emits the 19.1.1 <c>ready</c> line, answers <c>negotiate_protocol</c> and
    /// <c>get_state</c> with the frozen 19.1.1 response lines re-keyed to the session's ids, and defers every
    /// other command to <paramref name="scripted"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "CA2000",
        Justification = "The OmpRpcSession takes ownership of the transport and disposes it in DisposeAsync.")]
    private static async Task RunAsync(
        Func<string, string, IEnumerable<string>?> scripted,
        Func<OmpRpcSession, Task> body,
        CancellationToken ct)
    {
        IReadOnlyList<string> observed = RpcTestData.Observed19();
        var transport = new FakeRpcTransport(line =>
        {
            string command = RpcTestData.TypeOf(line);
            string id = RpcTestData.IdOf(line);
            return command switch
            {
                // Frozen 19.1.1 capture seq 4 (R-NEGOTIATE) and seq 6 (get_state), re-keyed only.
                "negotiate_protocol" or "get_state" =>
                    [RpcTestData.WithId(ObservedResponse(observed, command), id)],
                _ => scripted(command, id) ?? [],
            };
        });
        // Frozen 19.1.1 capture seq 1 (R-READY).
        transport.Emit(observed[0]);

        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        await session.CreateAsync(ct).ConfigureAwait(true);
        await body(session).ConfigureAwait(true);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// The live capture's prompt admission (seq 5: a bare success response with no <c>data</c>), re-keyed.
    /// </summary>
    private static string PromptAdmission(string id) =>
        "{\"id\":\"" + id + "\",\"type\":\"response\",\"command\":\"prompt\",\"success\":true}";

    /// <summary>The first live-capture <c>message_update</c> line carrying <paramref name="kind"/>, verbatim.</summary>
    private static string LiveDelta(string kind) =>
        RpcTestData.LiveCapture().First(line =>
        {
            JsonNode node = JsonNode.Parse(line)!;
            return string.Equals(node["type"]?.GetValue<string>(), "message_update", StringComparison.Ordinal)
                && string.Equals(node["assistantMessageEvent"]?["type"]?.GetValue<string>(), kind, StringComparison.Ordinal);
        });

    /// <summary>The first live-capture line of frame type <paramref name="type"/>, verbatim.</summary>
    private static string LiveFrame(IReadOnlyList<string> live, string type) =>
        live.First(line => string.Equals(JsonNode.Parse(line)!["type"]?.GetValue<string>(), type, StringComparison.Ordinal));

    /// <summary>The live capture's first <c>prompt_result</c> (seq 43, completed), re-keyed to <paramref name="id"/>.</summary>
    private static string LivePromptResult(string id) =>
        RpcTestData.WithId(LiveFrame(RpcTestData.LiveCapture(), "prompt_result"), id);

    private static string ObservedResponse(IReadOnlyList<string> observed, string command) =>
        observed.First(line => string.Equals(JsonNode.Parse(line)!["command"]?.GetValue<string>(), command, StringComparison.Ordinal));

    private static async Task<List<HarnessEvent>> CollectAsync(IAsyncEnumerable<HarnessEvent> events)
    {
        var collected = new List<HarnessEvent>();
        IAsyncEnumerator<HarnessEvent> enumerator = events.GetAsyncEnumerator();
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                collected.Add(enumerator.Current);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        return collected;
    }
}
