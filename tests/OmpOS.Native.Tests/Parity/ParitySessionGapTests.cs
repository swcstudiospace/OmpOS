using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using OmpOS.Native.Tests.RunControls;
using OmpOS.Native.Views;
using OmpOS.Parity.Support;
using OmpOS.Presentation.State;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Parity;

/// <summary>
/// Native checks that close the session rows of the Phase 21 parity inventory the Avalonia head did not draw: the lifecycle notices, the
/// stop, retry and jump-to-latest controls, and the transcript's follow and pause of its scroll position. Each draws a session view bound
/// to a model, or the shell's transcript where the report goes to the shell.
/// </summary>
public sealed class ParitySessionGapTests
{
    private const string ApprovalCopy = "Approval needed to continue. The decision is made by policy.";
    private const string ReconnectingCopy = "Reconnecting…";
    private const string OfflineCopy = "You are offline. Showing the last known output.";

    /// <summary>Row 24: while a turn awaits approval, the transcript says the policy decides, and offers no control that acts on it.</summary>
    [Fact]
    public async Task AwaitingApprovalDrawsAReadOnlyNotice()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new SessionViewModel();
            model.MarkAwaitingApproval();
            var (window, view) = ShowSession(model);

            var notice = view.FindControl<TextBlock>("StateNotice")!;
            notice.IsVisible.ShouldBeTrue("the approval notice is drawn");
            notice.Text.ShouldBe(ApprovalCopy);
            view.GetVisualDescendants().OfType<Button>().Where(button => button.IsVisible)
                .ShouldBeEmpty("the notice is read-only: no control acts on the approval");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row 25: while the transport reconnects, the transcript says so.</summary>
    [Fact]
    public async Task ReconnectingDrawsItsNotice()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new SessionViewModel();
            model.MarkReconnecting();
            var (window, view) = ShowSession(model);

            var notice = view.FindControl<TextBlock>("StateNotice")!;
            notice.IsVisible.ShouldBeTrue("the reconnecting notice is drawn");
            notice.Text.ShouldBe(ReconnectingCopy);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row 26: while the transport is offline, the transcript says so.</summary>
    [Fact]
    public async Task OfflineDrawsItsNotice()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new SessionViewModel();
            model.MarkOffline();
            var (window, view) = ShowSession(model);

            var notice = view.FindControl<TextBlock>("StateNotice")!;
            notice.IsVisible.ShouldBeTrue("the offline notice is drawn");
            notice.Text.ShouldBe(OfflineCopy);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row 27: while a turn streams, the transcript offers a stop control, and pressing it raises the stop request.</summary>
    [Fact]
    public async Task StreamingTurnOffersStopInTheTranscript()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new SessionViewModel();
            model.ApplyEvent(ParityEvents.Create(1, ParityEvents.Delta("working")));
            var (window, view) = ShowSession(model);
            var requests = 0;
            view.StopRequested += (_, _) => requests++;

            var stop = view.FindControl<Button>("SessionStopButton")!;
            stop.IsVisible.ShouldBeTrue("a streaming turn offers stop");
            AutomationProperties.GetName(stop).ShouldBe("Stop response");
            RunControlsHarness.Click(stop);
            requests.ShouldBe(1);

            model.ApplyEvent(ParityEvents.Create(2, ParityEvents.Completed()));
            ParityShellHarness.Settle(window);
            stop.IsVisible.ShouldBeFalse("a finished turn offers no stop");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row 28: after a recoverable failure, the transcript offers retry, and pressing it raises the retry request.</summary>
    [Fact]
    public async Task RecoverableFailureOffersRetryInTheTranscript()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new SessionViewModel();
            model.ApplyEvent(ParityEvents.Create(1, ParityEvents.Delta("partial")));
            model.ApplyEvent(ParityEvents.Create(2, ParityEvents.Error("transient", retryable: true)));
            var (window, view) = ShowSession(model);
            var requests = 0;
            view.RetryRequested += (_, _) => requests++;

            var retry = view.FindControl<Button>("SessionRetryButton")!;
            retry.IsVisible.ShouldBeTrue("a recoverable failure offers retry");
            AutomationProperties.GetName(retry).ShouldBe("Retry failed operation");
            RunControlsHarness.Click(retry);
            requests.ShouldBe(1);

            model.ApplyEvent(ParityEvents.Create(3, ParityEvents.Completed()));
            ParityShellHarness.Settle(window);
            retry.IsVisible.ShouldBeFalse("a completed turn offers no retry");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row 29: while auto-scroll is paused, the transcript offers jump to latest, and pressing it resumes following.</summary>
    [Fact]
    public async Task PausedTranscriptOffersJumpToLatest()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new SessionViewModel();
            model.MarkHistoryLoaded();
            var (window, view) = ShowSession(model);
            var jump = view.FindControl<Button>("JumpToLatestButton")!;
            jump.IsVisible.ShouldBeFalse("following the output offers no jump");

            model.PauseAutoScroll();
            ParityShellHarness.Settle(window);
            jump.IsVisible.ShouldBeTrue("a paused transcript offers jump to latest");
            AutomationProperties.GetName(jump).ShouldBe("Jump to latest");

            RunControlsHarness.Click(jump);
            ParityShellHarness.Settle(window);
            model.AutoScrollPaused.ShouldBeFalse("jump to latest resumes following");
            jump.IsVisible.ShouldBeFalse();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Row 30: while auto-scroll follows, the newest output is in view. Scrolling away from it pauses following, output that arrives while
    /// paused leaves the viewport where it is, and returning to the end resumes following.
    /// </summary>
    [Fact]
    public async Task AutoScrollFollowsOutputUntilTheUserScrollsAway()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new SessionViewModel();
            model.MarkHistoryLoaded();
            var (window, view) = ShowSession(model, height: 320);
            var scroll = view.FindControl<ScrollViewer>("TranscriptScroll")!;

            ParityShellHarness.AppendMessages(model, 1, 40);
            ParityShellHarness.Settle(window);
            scroll.Extent.Height.ShouldBeGreaterThan(scroll.Viewport.Height, "the transcript must overflow to scroll");
            ParityShellHarness.IsAtLatest(scroll).ShouldBeTrue("following draws the newest output");

            scroll.Offset = new Vector(0, 0);
            ParityShellHarness.Settle(window);
            model.AutoScrollPaused.ShouldBeTrue("scrolling away from the end pauses following");

            ParityShellHarness.AppendMessages(model, 41, 41);
            ParityShellHarness.Settle(window);
            scroll.Offset.Y.ShouldBe(0d, "output that arrives while paused leaves the viewport where it is");

            scroll.Offset = new Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
            ParityShellHarness.Settle(window);
            model.AutoScrollPaused.ShouldBeFalse("returning to the end resumes following");
            ParityShellHarness.IsAtLatest(scroll).ShouldBeTrue("the viewport is back at the newest output");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Row 43: each change of the transcript's scroll pause reaches the shell's session model once, and a scroll that stays paused reports
    /// nothing. Jump to latest reports the resume.
    /// </summary>
    [Fact]
    public async Task ScrollTransitionsReachTheShellOncePerChange()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var shell = ParityShellHarness.CreateMain(prompts: null);
            await using var shellScope = shell.ConfigureAwait(true);
            var window = ParityShellHarness.Show(shell);

            ParityShellHarness.AppendMessages(shell.Session, 1, 40);
            ParityShellHarness.Settle(window);
            var scroll = ParityShellHarness.Scroll(shell);
            var reported = new List<bool>();
            shell.Session.PropertyChanged += (_, e) =>
            {
                if (string.Equals(e.PropertyName, nameof(SessionViewModel.AutoScrollPaused), StringComparison.Ordinal))
                {
                    reported.Add(shell.Session.AutoScrollPaused);
                }
            };

            scroll.Offset = new Vector(0, 0);
            ParityShellHarness.Settle(window);
            scroll.Offset = new Vector(0, 0);
            ParityShellHarness.Settle(window);
            RunControlsHarness.Click(ParityShellHarness.Transcript(shell).FindControl<Button>("JumpToLatestButton")!);
            ParityShellHarness.Settle(window);

            reported.ShouldBe([true, false], "each transition is reported once, and a scroll that stays paused is not");
            ParityShellHarness.IsAtLatest(scroll).ShouldBeTrue("jump to latest returns to the newest output");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// The transcript follows its model only while it is shown. A model change made while it is hidden does not redraw it, and showing
    /// it again draws the state the model has reached.
    /// </summary>
    [Fact]
    public async Task HiddenTranscriptStopsFollowingItsModelUntilShownAgain()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new SessionViewModel();
            model.MarkReconnecting();
            var (window, view) = ShowSession(model);
            var notice = view.FindControl<TextBlock>("StateNotice")!;
            notice.Text.ShouldBe(ReconnectingCopy);

            window.Content = null;
            ParityShellHarness.Settle(window);
            model.MarkOffline();
            notice.Text.ShouldBe(ReconnectingCopy, "a hidden transcript does not follow its model");

            window.Content = view;
            ParityShellHarness.Settle(window);
            notice.Text.ShouldBe(OfflineCopy, "showing the transcript again draws the model's current state");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Shows a session view bound to the model, in a window of the given height, and settles it.</summary>
    private static (Window Window, SessionView View) ShowSession(SessionViewModel model, double height = 480)
    {
        var view = new SessionView { DataContext = model };
        var window = new Window { Content = view, Width = 640, Height = height };
        window.Show();
        ParityShellHarness.Settle(window);
        return (window, view);
    }
}
