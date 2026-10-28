using Bunit;
using Autonogrammer.GenUi;
using Autonogrammer.Presentation.State;
using Autonogrammer.UI.Components;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>Auto-follow, user scroll-up pause, and jump-to-latest resume through the scroll module (UI-02).</summary>
public sealed class SessionScrollTests : UiTestContext
{
    private const string ScrollModule = "./_content/Autonogrammer.UI/scripts/autonogrammer-scroll.js";
    private const string ScrollToEnd = "scrollToEnd";
    private const string JumpSelector = "button[aria-label=\"Jump to latest\"]";

    /// <summary>Following output moves the viewport to the latest content on every render.</summary>
    [Fact]
    public void FollowingOutputScrollsToLatestOnEveryRender()
    {
        var module = JSInterop.SetupModule(ScrollModule);
        var cut = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, UiTree.Empty)
            .Add(p => p.ViewState, SessionViewState.Streaming)
            .Add(p => p.AutoScrollPaused, false));

        cut.Render(parameters => parameters.Add(p => p.Announcement, "Streaming"));

        module.VerifyInvoke(ScrollToEnd, 2);
    }

    /// <summary>While the host has paused auto-scroll, new output does not move the viewport.</summary>
    [Fact]
    public void PausedOutputLeavesTheViewportWhereItIs()
    {
        var module = JSInterop.SetupModule(ScrollModule);
        var cut = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, UiTree.Empty)
            .Add(p => p.ViewState, SessionViewState.Streaming)
            .Add(p => p.AutoScrollPaused, true));

        cut.Render(parameters => parameters.Add(p => p.Announcement, "Streaming"));

        module.VerifyNotInvoke(ScrollToEnd);
    }

    /// <summary>A user scroll-up stops following and shows jump-to-latest; the jump resumes following and scrolls to the latest output.</summary>
    [Fact]
    public async Task UserScrollUpPausesFollowUntilJumpToLatestResumes()
    {
        var module = JSInterop.SetupModule(ScrollModule);
        var jumps = 0;
        var cut = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, UiTree.Empty)
            .Add(p => p.ViewState, SessionViewState.Streaming)
            .Add(p => p.AutoScrollPaused, false)
            .Add(p => p.OnJumpToLatest, () => jumps++));
        module.VerifyInvoke(ScrollToEnd, 1);

        await cut.InvokeAsync(() => cut.Instance.ReportScrollPositionAsync(true));
        cut.FindAll(JumpSelector).Count.ShouldBe(1);

        cut.Render(parameters => parameters.Add(p => p.Announcement, "More output"));
        module.VerifyInvoke(ScrollToEnd, 1);

        await cut.Find(JumpSelector).ClickAsync();

        jumps.ShouldBe(1);
        cut.FindAll(JumpSelector).Count.ShouldBe(0);
        module.VerifyInvoke(ScrollToEnd, 2);
    }

    /// <summary>Scroll transitions across the bottom edge are reported to the host once per change.</summary>
    [Fact]
    public async Task ScrollTransitionsAreReportedToTheHostOncePerChange()
    {
        JSInterop.SetupModule(ScrollModule);
        var changes = new List<bool>();
        var cut = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, UiTree.Empty)
            .Add(p => p.ViewState, SessionViewState.Streaming)
            .Add(p => p.AutoScrollPaused, false)
            .Add(p => p.AutoScrollPausedChanged, (bool paused) => changes.Add(paused)));

        await cut.InvokeAsync(() => cut.Instance.ReportScrollPositionAsync(true));
        await cut.InvokeAsync(() => cut.Instance.ReportScrollPositionAsync(true));
        await cut.InvokeAsync(() => cut.Instance.ReportScrollPositionAsync(false));

        changes.Count.ShouldBe(2);
        changes[0].ShouldBeTrue();
        changes[1].ShouldBeFalse();
        cut.FindAll(JumpSelector).Count.ShouldBe(0);
    }
}
