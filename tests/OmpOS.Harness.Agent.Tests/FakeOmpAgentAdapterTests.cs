using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Agent.Tests;

/// <summary>The adapter's admission and pump: a refused send reaches its caller, and a failed stream ends the reader.</summary>
public sealed class FakeOmpAgentAdapterTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A send made before the session is started is refused by the session's state machine (Disconnected cannot move to
    /// Streaming), and the refusal throws to the caller. No event is appended for the refused turn.
    /// </summary>
    [Fact]
    public async Task SendInANonReadyStateSurfacesTheRefusalToTheCaller()
    {
        var ct = TestContext.Current.CancellationToken;
        RecordedRpcCapture capture = RecordedRpcCapture.Load(LiveCaptureReplay.LiveCapture);
        FakeOmpAgentAdapter adapter = LiveCaptureReplay.Create(capture, LiveCaptureReplay.LiveSessionId);
        try
        {
            await Should.ThrowAsync<InvalidHarnessTransitionException>(() =>
                adapter.SendAsync(LiveCaptureReplay.Prompt, new PromptOptions(), "C-early", ct)).ConfigureAwait(true);
        }
        finally
        {
            await adapter.DisposeAsync().ConfigureAwait(true);
        }

        List<OmpEvent> events = await LiveCaptureReplay.CollectAsync(adapter.ReadEventsAsync(0, ct), ct).ConfigureAwait(true);
        events.ShouldBeEmpty();
    }

    /// <summary>
    /// A stream that fails mid-turn is observed by the adapter: its exception is kept as <see cref="FakeOmpAgentAdapter.PumpFault"/>
    /// with its type and message, a terminal error ends the reader, and the adapter then refuses any further turn. The reader's
    /// wait is bounded so a regression fails rather than hangs.
    /// </summary>
    [Fact]
    public async Task AFailingPumpEndsTheReaderWithATerminalError()
    {
        var ct = TestContext.Current.CancellationToken;
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(Bound);
        var session = new FaultingSession();
        try
        {
            var adapter = new FakeOmpAgentAdapter(session, "session-fault", new FakeTimeProvider());
            try
            {
                await adapter.SendAsync("prompt", new PromptOptions(), "C-fault", bound.Token).ConfigureAwait(true);

                List<OmpEvent> events = await LiveCaptureReplay.CollectAsync(adapter.ReadEventsAsync(0, bound.Token), bound.Token).ConfigureAwait(true);

                events.Count.ShouldBe(2);
                events[0].Payload.ShouldBe(new TextDeltaEvent("partial"));
                events[1].Payload.ShouldBeOfType<ErrorEvent>();
                events.All(e => string.Equals(e.CorrelationId, "C-fault", StringComparison.Ordinal)).ShouldBeTrue();

                Exception? fault = adapter.PumpFault;
                fault.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("The engine stream failed mid-turn.");

                await Should.ThrowAsync<InvalidOperationException>(() =>
                    adapter.SendAsync("next", new PromptOptions(), "C-after-fault", bound.Token))
                    .ConfigureAwait(true);
            }
            finally
            {
                await adapter.DisposeAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }
}
