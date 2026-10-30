using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Agent.Tests;

/// <summary>
/// Replays the recorded RPC and ACP transcripts through the real omp session parsers, behind the fake agent adapter.
/// </summary>
public sealed class RecordedTranscriptReplayTests
{
    private const string CommittedCapture = "rpc-out.ndjson";
    private const string AcpCapture = "acp-out.ndjson";
    private const string AcpRequests = "acp-in.ndjson";
    private const string AcpSessionId = "01a1582d-a7c8-724b-a542-695b31c7d7b4";

    /// <summary>Replaying the live capture twice yields identical event sequences: the same sequences, kinds and payloads.</summary>
    [Fact]
    public async Task ReplayingTheLiveCaptureTwiceYieldsIdenticalEventSequences()
    {
        var ct = TestContext.Current.CancellationToken;

        LiveCaptureReplay.LiveReplay first = await LiveCaptureReplay.ReplayLiveAsync(ct).ConfigureAwait(true);
        LiveCaptureReplay.LiveReplay second = await LiveCaptureReplay.ReplayLiveAsync(ct).ConfigureAwait(true);

        first.Events.ShouldNotBeEmpty();
        second.Events.Select(e => e.Sequence).ShouldBe(first.Events.Select(e => e.Sequence));
        second.Events.Select(e => e.Kind).ShouldBe(first.Events.Select(e => e.Kind));
        second.Events.Select(e => e.Payload).ShouldBe(first.Events.Select(e => e.Payload));
        second.Events.ShouldBe(first.Events);
        first.Events.Select(e => e.Sequence).ShouldBe(Enumerable.Range(1, first.Events.Count).Select(static index => (long)index));
    }

    /// <summary>Each replayed turn ends with the stop reason the sidecar recorded, and the replay yields text and thought events.</summary>
    [Fact]
    public async Task EachReplayedTurnEndsWithItsRecordedStopReason()
    {
        var ct = TestContext.Current.CancellationToken;
        LiveCaptureReplay.LiveReplay replay = await LiveCaptureReplay.ReplayLiveAsync(ct).ConfigureAwait(true);

        List<string> replayedReasons = replay.Events
            .Where(e => e.Payload is CompletedEvent)
            .Select(e => ((CompletedEvent)e.Payload).Reason)
            .ToList();
        List<string> recordedReasons = JsonNode.Parse(TranscriptFiles.ReadText(LiveCaptureReplay.LiveSidecar))!
            .AsArray()
            .Select(turn => turn!["stopReason"]!.GetValue<string>())
            .ToList();
        replayedReasons.ShouldBe(recordedReasons);
        replayedReasons.Count.ShouldBe(10);

        replay.Events.Select(e => e.Kind).ToHashSet().ShouldContain(OmpEventKind.TextDelta);
        replay.Events.Select(e => e.Kind).ToHashSet().ShouldContain(OmpEventKind.Thought);
    }

    /// <summary>The live capture carries ten <c>session_settled</c> frames outside any turn; <see cref="RecordedRpcCapture.Load"/> discards exactly those and counts them.</summary>
    [Fact]
    public void LiveCaptureDiscardsItsTenFramesOutsideTurnsAndCountsThem()
    {
        RecordedRpcCapture capture = RecordedRpcCapture.Load(LiveCaptureReplay.LiveCapture);

        capture.TurnCount.ShouldBe(10);
        capture.DiscardedFrameCount.ShouldBe(10);
    }

    /// <summary>The committed RPC capture replays its handshake through the real session and produces no events.</summary>
    [Fact]
    public async Task CommittedRpcCaptureReplaysItsHandshakeWithoutEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        RecordedRpcCapture capture = RecordedRpcCapture.Load(CommittedCapture);
        capture.TurnCount.ShouldBe(0);
        capture.DiscardedFrameCount.ShouldBe(0);
        FakeOmpAgentAdapter adapter = LiveCaptureReplay.Create(capture, "rpc-out");

        await adapter.StartAsync(ct).ConfigureAwait(true);

        adapter.Session.State.ShouldBe(HarnessState.Ready);
        adapter.Session.AdvertisedCapabilities["model.id"].ShouldBe("glm-5.3-flash");
        adapter.Session.AdvertisedCapabilities["model.provider"].ShouldBe("zai");
        await adapter.DisposeAsync().ConfigureAwait(true);
        List<OmpEvent> events = await LiveCaptureReplay.CollectAsync(adapter.ReadEventsAsync(0, ct), ct).ConfigureAwait(true);
        events.ShouldBeEmpty();
    }

    /// <summary>
    /// Cancelling a held turn leaves the session running: its state is Ready, the cancelled turn ends as cancelled, and the
    /// next send is accepted and streams a full turn with sequences that continue after the cancel. The abort reply and the
    /// <c>aborted</c> status are synthesised by repository convention (see <see cref="RecordedRpcCapture"/>), not observed omp
    /// output, so this test proves the session's handling of them, not omp's.
    /// </summary>
    [Fact]
    public async Task CancelTurnLeavesTheSessionRunningAndAcceptsTheNextTurn()
    {
        var ct = TestContext.Current.CancellationToken;
        RecordedRpcCapture capture = RecordedRpcCapture.Load(LiveCaptureReplay.LiveCapture);
        FakeOmpAgentAdapter adapter = LiveCaptureReplay.Create(capture, LiveCaptureReplay.LiveSessionId);
        using var readerCancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IAsyncEnumerator<OmpEvent> cursor = adapter.ReadEventsAsync(0, readerCancel.Token).GetAsyncEnumerator(readerCancel.Token);
        try
        {
            await adapter.StartAsync(ct).ConfigureAwait(true);
            capture.HoldNextTurn();
            await adapter.SendAsync(LiveCaptureReplay.Prompt, new PromptOptions(), "C-cancelled", ct).ConfigureAwait(true);

            var cancelled = new List<OmpEvent>();
            (await cursor.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
            cancelled.Add(cursor.Current);
            cursor.Current.Payload.ShouldNotBeOfType<CompletedEvent>();

            await adapter.CancelTurnAsync(ct).ConfigureAwait(true);
            OmpEvent cancelEnd = await LiveCaptureReplay.ReadThroughCompletedAsync(cursor, cancelled, readerCancel, ct).ConfigureAwait(true);

            cancelEnd.Payload.ShouldBe(new CompletedEvent("cancelled"));
            cancelled.All(e => string.Equals(e.CorrelationId, "C-cancelled", StringComparison.Ordinal)).ShouldBeTrue();
            adapter.Session.State.ShouldBe(HarnessState.Ready);

            await adapter.SendAsync(LiveCaptureReplay.Prompt, new PromptOptions(), "C-next", ct).ConfigureAwait(true);
            var next = new List<OmpEvent>();
            OmpEvent nextEnd = await LiveCaptureReplay.ReadThroughCompletedAsync(cursor, next, readerCancel, ct).ConfigureAwait(true);

            nextEnd.Payload.ShouldBe(new CompletedEvent("completed"));
            next.Count.ShouldBeGreaterThan(1);
            next.All(e => string.Equals(e.CorrelationId, "C-next", StringComparison.Ordinal)).ShouldBeTrue();
            next[0].Sequence.ShouldBe(cancelEnd.Sequence + 1);
            adapter.Session.State.ShouldBe(HarnessState.Ready);
        }
        finally
        {
            await cursor.DisposeAsync().ConfigureAwait(true);
            await adapter.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Handshake-only check, not replay evidence. The recorded handshake, answered through the real ACP session, starts it in
    /// the Ready state with the session identifier the capture recorded, and the captured request methods are sent in order.
    /// The ACP replay criterion is UNMET: acp-out.ndjson holds only initialize and session/new, and acp-in.ndjson has no
    /// session/prompt, so no prompt turn is replayed and no session event is checked here.
    /// </summary>
    [Fact]
    public async Task AcpHandshakeOnlyStartsTheSessionWithTheCapturedSessionId()
    {
        var ct = TestContext.Current.CancellationToken;
        RecordedAcpHandshake handshake = RecordedAcpHandshake.Load(AcpCapture);
        var transport = new ScriptedAcpTransport(handshake.Respond);
        var session = new AcpAgentSession(transport, new AcpClientOptions { AuthMethodId = "agent" });
        try
        {
            var adapter = new FakeOmpAgentAdapter(session, AcpSessionId, new FakeTimeProvider());
            try
            {
                await adapter.StartAsync(ct).ConfigureAwait(true);

                session.State.ShouldBe(HarnessState.Ready);
                session.SessionId.ShouldBe(AcpSessionId);
                TranscriptFiles.ReadLines(AcpCapture)
                    .Select(line => JsonNode.Parse(line)!["result"]?["sessionId"]?.GetValue<string>())
                    .ShouldContain(AcpSessionId, StringComparer.Ordinal);
                transport.SentMethods().ShouldBe(CapturedRequestMethods());
            }
            finally
            {
                await adapter.DisposeAsync().ConfigureAwait(true);
                // AcpAgentSession only closes the transport it is given and never disposes it, so the test owns its disposal.
                await transport.DisposeAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static List<string> CapturedRequestMethods() =>
        TranscriptFiles.ReadLines(AcpRequests)
            .Select(line => JsonNode.Parse(line)!["method"]!.GetValue<string>())
            .ToList();
}
