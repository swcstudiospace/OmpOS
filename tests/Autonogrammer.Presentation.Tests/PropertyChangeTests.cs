using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.State;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>
/// Change notifications on the presentation models (OQ-44): a bound view learns about each state change, derived
/// properties follow their source, and a write that leaves a value unchanged announces nothing.
/// </summary>
public sealed class PropertyChangeTests
{
    private const string SessionId = "s-1";
    private const string CorrelationId = "s-1/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 20, 9, 30, 0, TimeSpan.Zero);

    /// <summary>A streamed delta announces the tree, the lifecycle state, the derived stop control and the announcement.</summary>
    [Fact]
    public void StreamedDeltaAnnouncesTreeStateAndStopControl()
    {
        var model = new SessionViewModel();
        var changed = PropertyChangeRecorder.Record(model);

        model.ApplyEvent(Stream(new TextDeltaPayload { Delta = "hi" }));

        changed.ShouldAnnounce(nameof(SessionViewModel.Tree));
        changed.ShouldAnnounce(nameof(SessionViewModel.State));
        changed.ShouldAnnounce(nameof(SessionViewModel.CanStop));
        changed.ShouldAnnounce(nameof(SessionViewModel.Announcement));
    }

    /// <summary>A retryable error announces the derived retry control, which the view binds to.</summary>
    [Fact]
    public void RecoverableErrorAnnouncesTheRetryControl()
    {
        var model = new SessionViewModel();
        var changed = PropertyChangeRecorder.Record(model);

        model.ApplyEvent(Stream(new ErrorPayload { Message = "transient", Retryable = true }));

        changed.ShouldAnnounce(nameof(SessionViewModel.CanRetry));
        changed.ShouldAnnounce(nameof(SessionViewModel.LastError));
    }

    /// <summary>A diagnostic has no view surface, so it announces nothing.</summary>
    [Fact]
    public void DiagnosticAnnouncesNothing()
    {
        var model = new SessionViewModel();
        var changed = PropertyChangeRecorder.Record(model);

        model.ApplyEvent(Stream(new DiagnosticPayload { Message = "slow tool" }));

        changed.ShouldBeEmpty();
    }

    /// <summary>Pausing auto-scroll twice announces the change once: a repeated value raises nothing.</summary>
    [Fact]
    public void RepeatedAutoScrollPauseAnnouncesOnce()
    {
        var model = new SessionViewModel();
        var changed = PropertyChangeRecorder.Record(model);

        model.PauseAutoScroll();
        model.PauseAutoScroll();

        changed.ShouldBe([nameof(SessionViewModel.AutoScrollPaused)]);
    }

    /// <summary>Setting the draft and the streaming flag announces each change; setting the same draft again announces nothing.</summary>
    [Fact]
    public void DraftAndStreamingFlagAnnounceTheirChanges()
    {
        var model = new ComposerViewModel(_ => Task.FromResult(true), () => Task.CompletedTask);
        var changed = PropertyChangeRecorder.Record(model);

        model.Draft = "hello";
        model.Draft = "hello";
        model.IsStreaming = true;

        changed.ShouldBe([nameof(ComposerViewModel.Draft), nameof(ComposerViewModel.IsStreaming)]);
    }

    /// <summary>A send the host accepts clears the draft, and the clearing is announced to the bound input.</summary>
    [Fact]
    public async Task AcceptedSendAnnouncesTheClearedDraft()
    {
        var model = new ComposerViewModel(_ => Task.FromResult(true), () => Task.CompletedTask) { Draft = "hello" };
        var changed = PropertyChangeRecorder.Record(model);

        await model.SendAsync();

        model.Draft.ShouldBeEmpty();
        changed.ShouldBe([nameof(ComposerViewModel.Draft)]);
    }

    private static StreamEvent Stream(StreamEventPayload payload) => new()
    {
        Seq = 1,
        SessionId = SessionId,
        CorrelationId = CorrelationId,
        Timestamp = Stamp,
        Payload = payload,
    };
}
