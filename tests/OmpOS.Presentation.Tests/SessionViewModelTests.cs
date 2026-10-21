using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.Presentation.State;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>
/// Lifecycle transitions, the scroll-pause flag, announcements, and the awaiting-approval rule, tested on
/// <see cref="SessionViewModel"/> with no UI framework (UI-02, XP-08). The bUnit originals stay in OmpOS.UI.Tests.
/// </summary>
public sealed class SessionViewModelTests
{
    private const string SessionId = "s-1";
    private const string CorrelationId = "s-1/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 20, 9, 30, 0, TimeSpan.Zero);

    /// <summary>A text delta moves the model to streaming with the stop affordance and no retry.</summary>
    [Fact]
    public void TextDeltaStreamsWithStopAffordance()
    {
        var model = new SessionViewModel();

        model.ApplyEvent(Stream(new TextDeltaPayload { Delta = "hi" }));

        model.State.ShouldBe(SessionViewState.Streaming);
        model.CanStop.ShouldBeTrue();
        model.CanRetry.ShouldBeFalse();
    }

    /// <summary>A retryable error preserves partial output and offers retry.</summary>
    [Fact]
    public void RetryableErrorPreservesPartialOutputWithRetry()
    {
        var model = new SessionViewModel();
        model.ApplyEvent(Stream(new TextDeltaPayload { Delta = "partial" }));

        model.ApplyEvent(Stream(new ErrorPayload { Message = "transient", Retryable = true }));

        model.State.ShouldBe(SessionViewState.RecoverableError);
        model.CanRetry.ShouldBeTrue();
        model.CanStop.ShouldBeFalse();
        model.LastError.ShouldBe("transient");
        model.Tree.Blocks.OfType<MarkdownBlock>().Single().Markdown.ShouldBe("partial");
    }

    /// <summary>A fatal error preserves partial output without retry.</summary>
    [Fact]
    public void FatalErrorPreservesPartialOutputWithoutRetry()
    {
        var model = new SessionViewModel();
        model.ApplyEvent(Stream(new TextDeltaPayload { Delta = "partial" }));

        model.ApplyEvent(Stream(new ErrorPayload { Message = "fatal", Retryable = false }));

        model.State.ShouldBe(SessionViewState.FatalError);
        model.CanRetry.ShouldBeFalse();
        model.CanStop.ShouldBeFalse();
        model.Tree.Blocks.OfType<MarkdownBlock>().Single().Markdown.ShouldBe("partial");
    }

    /// <summary>Completion moves the model to success and flushes streamed text into the tree.</summary>
    [Fact]
    public void CompletionMovesToSuccess()
    {
        var model = new SessionViewModel();
        model.ApplyEvent(Stream(new TextDeltaPayload { Delta = "done" }));

        model.ApplyEvent(Stream(new CompletedPayload { Reason = "end_turn" }));

        model.State.ShouldBe(SessionViewState.Success);
        model.Tree.CompletionReason.ShouldBe("end_turn");
        model.Tree.StreamingText.ShouldBeEmpty();
    }

    /// <summary>A diagnostic is non-terminal: it keeps the streaming state, and after an error it leaves the error state in place.</summary>
    [Fact]
    public void DiagnosticNeverChangesLifecycleState()
    {
        var model = new SessionViewModel();
        model.ApplyEvent(Stream(new TextDeltaPayload { Delta = "partial" }));

        model.ApplyEvent(Stream(new DiagnosticPayload { Message = "slow tool" }));
        model.State.ShouldBe(SessionViewState.Streaming);

        model.ApplyEvent(Stream(new ErrorPayload { Message = "transient", Retryable = true }));
        model.ApplyEvent(Stream(new DiagnosticPayload { Message = "after error" }));

        model.State.ShouldBe(SessionViewState.RecoverableError);
        model.CanRetry.ShouldBeTrue();
    }

    /// <summary>Replaying a diagnostic leaves the tree untouched: no unknown card and no text split, matching the live feed which drops diagnostics before the view-model.</summary>
    [Fact]
    public void DiagnosticLeavesTreeUntouched()
    {
        var model = new SessionViewModel();
        model.ApplyEvent(Stream(new TextDeltaPayload { Delta = "hello " }));

        model.ApplyEvent(Stream(new DiagnosticPayload { Message = "slow tool" }));

        model.Tree.StreamingText.ShouldBe("hello ");
        model.Tree.Blocks.ShouldBeEmpty();

        model.ApplyEvent(Stream(new TextDeltaPayload { Delta = "world" }));

        model.Tree.StreamingText.ShouldBe("hello world");
        model.Tree.Blocks.ShouldBeEmpty();
        model.State.ShouldBe(SessionViewState.Streaming);
    }

    /// <summary>A log holding only diagnostics replays to the empty prompt, matching the live view which never shows them.</summary>
    [Fact]
    public void HistoryOfOnlyDiagnosticsLoadsEmpty()
    {
        var model = new SessionViewModel();
        model.ApplyEvent(Stream(new DiagnosticPayload { Message = "slow tool" }));
        model.MarkHistoryLoaded();

        model.State.ShouldBe(SessionViewState.Empty);
        model.Tree.Blocks.ShouldBeEmpty();
        model.Tree.StreamingText.ShouldBeEmpty();
    }

    /// <summary>History load resolves to empty when nothing folded, success otherwise.</summary>
    /// <param name="withEvents">Whether the history holds one streamed event.</param>
    /// <param name="expected">The expected lifecycle state after the history loads.</param>
    [Theory]
    [InlineData(false, SessionViewState.Empty)]
    [InlineData(true, SessionViewState.Success)]
    public void HistoryLoadResolvesEmptyOrSuccess(bool withEvents, SessionViewState expected)
    {
        var model = new SessionViewModel();
        if (withEvents)
        {
            model.ApplyEvent(Stream(new TextDeltaPayload { Delta = "x" }));
        }

        model.MarkHistoryLoaded();

        model.State.ShouldBe(expected);
    }

    /// <summary>Reconnecting and offline keep the last known tree visible.</summary>
    /// <param name="expected">The transport state under test.</param>
    [Theory]
    [InlineData(SessionViewState.Reconnecting)]
    [InlineData(SessionViewState.Offline)]
    public void TransportStatesKeepOutputVisible(SessionViewState expected)
    {
        var model = new SessionViewModel();
        model.ApplyEvent(Stream(new TextDeltaPayload { Delta = "kept" }));

        if (expected == SessionViewState.Reconnecting)
        {
            model.MarkReconnecting();
        }
        else
        {
            model.MarkOffline();
        }

        model.State.ShouldBe(expected);
        model.Tree.StreamingText.ShouldBe("kept");
    }

    /// <summary>The scroll-pause flag toggles on user scroll-up and jump-to-latest.</summary>
    [Fact]
    public void ScrollPauseFlagToggles()
    {
        var model = new SessionViewModel();

        model.AutoScrollPaused.ShouldBeFalse();
        model.PauseAutoScroll();
        model.AutoScrollPaused.ShouldBeTrue();
        model.ResumeAutoScroll();
        model.AutoScrollPaused.ShouldBeFalse();
    }

    /// <summary>Announcements change on state transitions, not on streaming deltas.</summary>
    [Fact]
    public void AnnouncementsThrottleToStateChanges()
    {
        var model = new SessionViewModel();

        model.ApplyEvent(Stream(new TextDeltaPayload { Delta = "a" }));
        var streaming = model.Announcement;
        model.ApplyEvent(Stream(new TextDeltaPayload { Delta = "b" }));

        string.Equals(model.Announcement, streaming, StringComparison.Ordinal).ShouldBeTrue();
        model.ApplyEvent(Stream(new CompletedPayload { Reason = "end_turn" }));
        string.Equals(model.Announcement, streaming, StringComparison.Ordinal).ShouldBeFalse();
    }

    /// <summary>A permission event keeps the turn streaming: it never enters AwaitingApproval, so the composer stays usable.</summary>
    [Fact]
    public void PermissionEventNeverEntersAwaitingApproval()
    {
        var model = new SessionViewModel();
        model.ApplyEvent(Stream(new TextDeltaPayload { Delta = "partial" }));

        model.ApplyEvent(Stream(PermissionPayload()));

        model.State.ShouldBe(SessionViewState.Streaming);
        model.State.ShouldNotBe(SessionViewState.AwaitingApproval);
        model.CanStop.ShouldBeTrue();
        model.CanRetry.ShouldBeFalse();
        model.Tree.Blocks.OfType<PermissionBlock>().Single().Options.ShouldBe(["allow once", "ask me"]);
    }

    /// <summary>Marking approval enters the state with its announcement and no stop or retry affordance (UI-02).</summary>
    [Fact]
    public void MarkAwaitingApprovalSetsStateAndAnnouncement()
    {
        var model = new SessionViewModel();
        model.MarkAwaitingApproval();

        model.State.ShouldBe(SessionViewState.AwaitingApproval);
        model.Announcement.ShouldBe("Approval needed to continue.");
        model.CanStop.ShouldBeFalse();
        model.CanRetry.ShouldBeFalse();
    }

    private static StreamEvent Stream(StreamEventPayload payload) => new()
    {
        Seq = 1,
        SessionId = SessionId,
        CorrelationId = CorrelationId,
        Timestamp = Stamp,
        Payload = payload,
    };

    private static UnknownPayload PermissionPayload()
    {
        var obj = new JsonObject
        {
            ["kind"] = "permission_request",
            ["callId"] = "call-9",
            ["name"] = "bash",
            ["payloadJson"] = "{\"options\":[\"allow once\",\"ask me\"]}",
        };
        using var doc = JsonDocument.Parse(obj.ToJsonString());
        return new UnknownPayload { OriginalKind = "permission_request", Raw = doc.RootElement.Clone() };
    }
}
