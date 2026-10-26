using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Rpc;
using Shouldly;

namespace OmpOS.Harness.Agent.Tests;

/// <summary>Replays recorded RPC captures through a real <see cref="OmpRpcSession"/> behind the fake adapter.</summary>
internal static class LiveCaptureReplay
{
    /// <summary>The live capture of 2026-10-21: ten turns, each completed.</summary>
    public const string LiveCapture = "rpc-live-20261021T171351Z.ndjson";

    /// <summary>The sidecar recording each live turn's stop reason.</summary>
    public const string LiveSidecar = "rpc-live-20261021T171351Z.turns.json";

    /// <summary>The session identifier used for the live capture.</summary>
    public const string LiveSessionId = "rpc-live-20261021T171351Z";

    /// <summary>The prompt the live capture's turns were sent with.</summary>
    public const string Prompt = "Reply with the single word ok.";

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The result of a full replay: the events of every turn, and the adapter that produced them, already disposed.</summary>
    /// <param name="Adapter">The disposed adapter; its log stays readable.</param>
    /// <param name="Events">Every event, in sequence order.</param>
    internal sealed record LiveReplay(FakeOmpAgentAdapter Adapter, List<OmpEvent> Events);

    /// <summary>Creates an adapter over a capture. The engine's connection frames are delivered before any command.</summary>
    /// <param name="capture">The capture to answer from.</param>
    /// <param name="sessionId">The session identifier to record.</param>
    public static FakeOmpAgentAdapter Create(RecordedRpcCapture capture, string sessionId) =>
        new FakeOmpAgentAdapter(NewSession(capture), sessionId, new FakeTimeProvider());

    private static OmpRpcSession NewSession(RecordedRpcCapture capture) =>
        new OmpRpcSession(NewTransport(capture), CommandTimeout, CommandTimeout);

    private static ScriptedRpcTransport NewTransport(RecordedRpcCapture capture) =>
        new ScriptedRpcTransport(capture.Respond, capture.ConnectionFrames);

    /// <summary>Replays the live capture: starts the session, sends every recorded turn, and disposes the adapter.</summary>
    /// <param name="cancellationToken">Cancels the replay.</param>
    public static async Task<LiveReplay> ReplayLiveAsync(CancellationToken cancellationToken)
    {
        RecordedRpcCapture capture = RecordedRpcCapture.Load(LiveCapture);
        FakeOmpAgentAdapter adapter = Create(capture, LiveSessionId);
        List<OmpEvent> events = await ReplayTurnsAsync(adapter, capture.TurnCount, cancellationToken).ConfigureAwait(false);
        return new LiveReplay(adapter, events);
    }

    /// <summary>Starts the adapter, sends <paramref name="turnCount"/> turns one after another, and disposes the adapter.</summary>
    /// <param name="adapter">The adapter to drive.</param>
    /// <param name="turnCount">The number of turns to send.</param>
    /// <param name="cancellationToken">Cancels the replay.</param>
    /// <returns>Every event read, in sequence order.</returns>
    public static async Task<List<OmpEvent>> ReplayTurnsAsync(FakeOmpAgentAdapter adapter, int turnCount, CancellationToken cancellationToken)
    {
        var events = new List<OmpEvent>();
        try
        {
            await adapter.StartAsync(cancellationToken).ConfigureAwait(false);
            using var readerCancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IAsyncEnumerator<OmpEvent> cursor = adapter.ReadEventsAsync(0, readerCancel.Token).GetAsyncEnumerator(readerCancel.Token);
            try
            {
                for (int turn = 1; turn <= turnCount; turn++)
                {
                    await adapter.SendAsync(Prompt, new PromptOptions(), $"C-{turn}", cancellationToken).ConfigureAwait(false);
                    _ = await ReadThroughCompletedAsync(cursor, events, readerCancel, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                await cursor.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await adapter.DisposeAsync().ConfigureAwait(false);
        }

        return events;
    }

    /// <summary>
    /// Reads events from the cursor, appending each to <paramref name="into"/>, up to and including the next
    /// <see cref="CompletedEvent"/>. Each read is bounded. When a read times out or is cancelled, the pending read is
    /// cancelled through <paramref name="readerCancel"/> and settled before the exception leaves, so the caller can dispose
    /// the cursor safely.
    /// </summary>
    /// <param name="cursor">An open read of the session's events, created with the token of <paramref name="readerCancel"/>.</param>
    /// <param name="into">The list that receives each event read.</param>
    /// <param name="readerCancel">Cancels the cursor's own read. Cancelled when a read gives up.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The completed event that ended the read.</returns>
    /// <exception cref="TimeoutException">No event arrived within the read timeout.</exception>
    public static async Task<OmpEvent> ReadThroughCompletedAsync(
        IAsyncEnumerator<OmpEvent> cursor,
        List<OmpEvent> into,
        CancellationTokenSource readerCancel,
        CancellationToken cancellationToken)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(ReadTimeout);
        while (true)
        {
            Task<bool> pending = cursor.MoveNextAsync().AsTask();
            bool more;
            try
            {
                more = await pending.WaitAsync(bound.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                await readerCancel.CancelAsync().ConfigureAwait(false);
                try
                {
                    await pending.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected: the cancelled read ends with cancellation.
                }

                if (bound.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"No event arrived within {ReadTimeout} during the replay.", ex);
                }

                throw;
            }

            more.ShouldBeTrue("the event stream ended before the turn completed");
            OmpEvent next = cursor.Current;
            into.Add(next);
            if (next.Payload is CompletedEvent)
            {
                return next;
            }
        }
    }

    /// <summary>Reads every event of a finished (or disposed) adapter from a position.</summary>
    /// <param name="events">The event stream to drain.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static async Task<List<OmpEvent>> CollectAsync(IAsyncEnumerable<OmpEvent> events, CancellationToken cancellationToken)
    {
        var collected = new List<OmpEvent>();
        IAsyncEnumerator<OmpEvent> enumerator = events.GetAsyncEnumerator(cancellationToken);
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
