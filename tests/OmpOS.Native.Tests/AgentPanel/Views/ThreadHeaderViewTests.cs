using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using OmpOS.Contracts.Agents;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentPanel.Views;

/// <summary>The thread header and its panel notice: title, chips, Close, Resume or Reopen (46-UI-SPEC, Control Inventory 10 to 21).</summary>
public sealed class ThreadHeaderViewTests
{
    private const string LongTitle = "Port the settings screen to the shared presentation layer and keep it accessible";

    private static ThreadHeaderView Header(AgentSessionViewModel? model, out Window window, bool reducedMotion = false, object? trailing = null)
    {
        var header = new ThreadHeaderView { DataContext = model, Trailing = trailing };
        window = ViewHarness.Show(header, reducedMotion);
        return header;
    }

    private static Button HeaderButton(ThreadHeaderView header, string name) => header.FindControl<Button>(name)!;

    private static PanelNoticeView NoticeOf(ThreadHeaderView header) => header.FindControl<PanelNoticeView>("PanelNoticeHost")!;

    private static Button NoticeButton(ThreadHeaderView header, string name) => NoticeOf(header).FindControl<Button>(name)!;

    private static SessionCatalogOptions ClosedOptions() => new() { IsClosed = _ => true };

    /// <summary>A running agent shows its title, status chip, task chip, graph chip and steering badge, each with its accessible name.</summary>
    [Fact]
    public async Task StreamingShowsStopAndSteer()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var clock = new ManualTimeProvider();
            var row = PanelFixtures.Row("s1", SessionLifecycle.Running, clock, title: "Build the rail");
            var scheduler = PanelFixtures.ActiveScheduler(new ScriptedRunInput());
            using var model = PanelFixtures.Session(
                row,
                clock,
                scheduler: scheduler,
                context: ScriptedContextSource.Returning(PanelFixtures.Context()),
                recorder: static (_, _) => Task.CompletedTask);
            await model.ActivateAsync().ConfigureAwait(true);
            var header = Header(model, out var window);

            var texts = ViewHarness.VisibleTexts(header);
            texts.ShouldContain("Build the rail", StringComparer.Ordinal);
            texts.ShouldContain("Running", StringComparer.Ordinal);
            texts.ShouldContain("Task: Ship the panel", StringComparer.Ordinal);
            texts.ShouldContain("Graph ut-mvp0bh59-2dd6ef4c", StringComparer.Ordinal);
            texts.ShouldContain(AgentPanelCopy.SteeringAvailable, StringComparer.Ordinal);

            AutomationProperties.GetName(header.FindControl<Border>("HeaderFrame")!).ShouldBe("Agent header");
            AutomationProperties.GetName(header.FindControl<TextBlock>("TitleText")!).ShouldBe("Agent title");
            AutomationProperties.GetName(header.FindControl<StatusChip>("StatusChipView")!).ShouldBe("Status: Running");
            AutomationProperties.GetName(HeaderButton(header, "TaskChipButton")).ShouldBe("Task Ship the panel. Open task context.");
            AutomationProperties.GetName(header.FindControl<Border>("GraphChipView")!).ShouldBe("Graph ut-mvp0bh59-2dd6ef4c");
            AutomationProperties.GetName(header.FindControl<Border>("SteeringBadge")!).ShouldBe(model.SteeringBadgeName);
            AutomationProperties.GetName(HeaderButton(header, "CloseButton")).ShouldBe("Close this agent");
            HeaderButton(header, "CloseButton").MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            HeaderButton(header, "CloseButton").MinWidth.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            HeaderButton(header, "ResumeButton").IsVisible.ShouldBeFalse();
            HeaderButton(header, "ReopenButton").IsVisible.ShouldBeFalse();
            ViewHarness.Unnamed(header).ShouldBeEmpty();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The steering badge reads the four texts of the copy contract as the session changes.</summary>
    [Fact]
    public async Task TheSteeringBadgeFollowsTheSession()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var row = PanelFixtures.Row("s1", SessionLifecycle.Running, clock);
            var scheduler = new OmpOS.Presentation.Runs.RunInputScheduler(new ScriptedRunInput());
            using var model = PanelFixtures.Session(row, clock, scheduler: scheduler);
            var header = Header(model, out var window);
            var label = header.FindControl<TextBlock>("SteeringBadgeLabel")!;

            label.Text.ShouldBe(AgentPanelCopy.SteeringIdle);

            scheduler.RunStarted();
            ViewHarness.Settle(window);
            label.Text.ShouldBe(AgentPanelCopy.SteeringAvailableNotRecorded);
            AutomationProperties.GetName(header.FindControl<Border>("SteeringBadge")!).ShouldBe(model.SteeringBadgeName);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The task chip cuts a long title at 28 characters and keeps the full title in its accessible name.</summary>
    [Fact]
    public async Task LongTextIsCapped()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var clock = new ManualTimeProvider();
            var row = PanelFixtures.Row("s1", SessionLifecycle.Running, clock, title: LongTitle);
            using var model = PanelFixtures.Session(row, clock, context: ScriptedContextSource.Returning(PanelFixtures.Context(title: LongTitle)));
            await model.ActivateAsync().ConfigureAwait(true);
            var header = Header(model, out var window);

            var chip = header.FindControl<TextBlock>("TaskChipLabel")!;
            chip.Text!.Length.ShouldBe("Task: ".Length + AgentPanelViewText.TaskChipChars);
            chip.Text.ShouldEndWith("\u2026");
            AutomationProperties.GetName(HeaderButton(header, "TaskChipButton")).ShouldNotBeNull().ShouldContain(LongTitle);
            header.FindControl<TextBlock>("TitleText")!.TextTrimming.ShouldBe(Avalonia.Media.TextTrimming.CharacterEllipsis);
            header.FindControl<TextBlock>("TitleText")!.MaxLines.ShouldBe(1);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A lost agent offers Resume agent in the header and in the notice, and resuming runs the host's resume.</summary>
    [Fact]
    public async Task LostSessionCanBeResumed()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var clock = new ManualTimeProvider();
            var row = PanelFixtures.Row("s1", SessionLifecycle.Lost, clock, title: "Long job");
            var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var model = PanelFixtures.Session(row, clock, resume: _ =>
            {
                resumed.TrySetResult();
                return Task.CompletedTask;
            });
            var header = Header(model, out var window);

            model.IsLost.ShouldBeTrue();
            var resume = HeaderButton(header, "ResumeButton");
            resume.IsEffectivelyVisible.ShouldBeTrue();
            resume.Classes.Contains("primary").ShouldBeTrue();
            AutomationProperties.GetName(resume).ShouldBe("Resume agent");
            HeaderButton(header, "ReopenButton").IsVisible.ShouldBeFalse("Resume and Reopen never show together");
            ViewHarness.VisibleTexts(header).ShouldContain($"{AgentPanelCopy.LostHeading} {AgentPanelCopy.LostBody}", StringComparer.Ordinal);
            NoticeButton(header, "ResumeButton").IsEffectivelyVisible.ShouldBeTrue();
            NoticeButton(header, "DismissButton").IsVisible.ShouldBeFalse("a lost notice stays until the agent is resumed");

            ViewHarness.Click(window, resume);
            await resumed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(true);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A closed agent offers Reopen agent, hides Close and reads Steering unavailable.</summary>
    [Fact]
    public async Task ClosedSessionShowsNotice()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var clock = new ManualTimeProvider();
            var row = PanelFixtures.Row("s1", SessionLifecycle.Paused, clock, title: "Done for now", options: ClosedOptions());
            var reopened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var model = PanelFixtures.Session(row, clock, reopen: _ =>
            {
                reopened.TrySetResult();
                return Task.CompletedTask;
            });
            var header = Header(model, out var window);

            model.IsClosed.ShouldBeTrue();
            var reopen = HeaderButton(header, "ReopenButton");
            reopen.IsEffectivelyVisible.ShouldBeTrue();
            AutomationProperties.GetName(reopen).ShouldBe("Reopen agent");
            HeaderButton(header, "ResumeButton").IsVisible.ShouldBeFalse();
            HeaderButton(header, "CloseButton").IsVisible.ShouldBeFalse();
            ViewHarness.VisibleTexts(header).ShouldContain("Closed", StringComparer.Ordinal);
            ViewHarness.VisibleTexts(header).ShouldContain(AgentPanelCopy.SteeringUnavailable, StringComparer.Ordinal);

            ViewHarness.Click(window, reopen);
            await reopened.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(true);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Close acts at once for an agent that is not running and asks for confirmation for one that is.</summary>
    [Fact]
    public async Task CloseAsksForConfirmationOnlyWhileRunning()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var idleRow = PanelFixtures.Row("idle", SessionLifecycle.Created, clock);
            using var idle = PanelFixtures.Session(idleRow, clock);
            var idleCloses = 0;
            idle.CloseRequested += (_, _) => idleCloses++;
            var header = Header(idle, out var window);
            var confirmations = 0;
            header.CloseClicked += (_, _) => confirmations++;

            ViewHarness.Click(window, HeaderButton(header, "CloseButton"));
            idleCloses.ShouldBe(1);
            confirmations.ShouldBe(0);
            window.Close();

            var runningRow = PanelFixtures.Row("busy", SessionLifecycle.Running, clock);
            using var busy = PanelFixtures.Session(runningRow, clock, scheduler: PanelFixtures.ActiveScheduler(new ScriptedRunInput()));
            var busyCloses = 0;
            busy.CloseRequested += (_, _) => busyCloses++;
            var busyHeader = Header(busy, out var busyWindow);
            busyHeader.CloseClicked += (_, _) => confirmations++;

            ViewHarness.Click(busyWindow, HeaderButton(busyHeader, "CloseButton"));
            busyCloses.ShouldBe(0, "a running agent is not closed before the bar is confirmed");
            confirmations.ShouldBe(1);
            busyWindow.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The task chip and the Show pipeline button tell the owner: the chip for its task, the button to show the pipeline rows.</summary>
    [Fact]
    public async Task ChipAndNoticeButtonsRaiseTheirEvents()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var clock = new ManualTimeProvider();
            var row = PanelFixtures.Row("s1", SessionLifecycle.Running, clock);
            using var model = PanelFixtures.Session(row, clock, context: ScriptedContextSource.Returning(PanelFixtures.Context()));
            await model.ActivateAsync().ConfigureAwait(true);
            model.ApplyStageEvent(PanelFixtures.Stage(StageSource.Ultrathink, "plan", StageStatus.Skipped, clock.GetUtcNow(), sessionId: "s1"));
            var header = Header(model, out var window);
            var taskClicks = 0;
            var pipelineClicks = 0;
            header.TaskChipClicked += (_, _) => taskClicks++;
            header.ShowPipelineRequested += (_, _) => pipelineClicks++;

            ViewHarness.Click(window, HeaderButton(header, "TaskChipButton"));
            ViewHarness.Click(window, NoticeButton(header, "ShowPipelineButton"));

            taskClicks.ShouldBe(1);
            pipelineClicks.ShouldBe(1);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A degraded stage shows the polite panel notice with its counted copy, Show pipeline and Dismiss notice; dismissing hides it.</summary>
    [Fact]
    public async Task TheDegradedNoticeIsAPoliteLiveRegionThatCanBeDismissed()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var row = PanelFixtures.Row("s1", SessionLifecycle.Running, clock);
            using var model = PanelFixtures.Session(row, clock);
            var header = Header(model, out var window);
            var notice = NoticeOf(header).FindControl<Border>("PanelNotice")!;

            notice.IsVisible.ShouldBeFalse("no notice without a degraded stage");

            model.ApplyStageEvent(PanelFixtures.Stage(StageSource.Ultrathink, "plan", StageStatus.Skipped, clock.GetUtcNow(), sessionId: "s1"));
            model.ApplyStageEvent(PanelFixtures.Stage(StageSource.Swarm, "run", StageStatus.Unavailable, clock.GetUtcNow(), sessionId: "s1"));
            ViewHarness.Settle(window);

            notice.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(notice).ShouldBe("Panel notice");
            AutomationProperties.GetLiveSetting(notice).ShouldBe(AutomationLiveSetting.Polite);
            ViewHarness.VisibleTexts(header).ShouldContain(AgentPanelCopy.DegradedNotice(2), StringComparer.Ordinal);
            NoticeButton(header, "ShowPipelineButton").IsEffectivelyVisible.ShouldBeTrue();
            AutomationProperties.GetName(NoticeButton(header, "ShowPipelineButton")).ShouldBe("Show pipeline details");
            AutomationProperties.GetName(NoticeButton(header, "DismissButton")).ShouldBe("Dismiss notice");
            NoticeButton(header, "ResumeButton").IsVisible.ShouldBeFalse();
            ViewHarness.Unnamed(header).ShouldBeEmpty();

            ViewHarness.Click(window, NoticeButton(header, "DismissButton"));
            notice.IsVisible.ShouldBeFalse();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>When no plugin is available the notice says the agent runs as plain omp.</summary>
    [Fact]
    public async Task NoPluginsStillRuns()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var row = PanelFixtures.Row("s1", SessionLifecycle.Running, clock);
            using var model = PanelFixtures.Session(row, clock);
            foreach (var source in new[] { StageSource.Ultrathink, StageSource.Swarm, StageSource.Substrate, StageSource.Gsd })
            {
                model.ApplyStageEvent(PanelFixtures.Stage(source, "check", StageStatus.Unavailable, clock.GetUtcNow(), sessionId: "s1"));
            }

            var header = Header(model, out var window);

            ViewHarness.VisibleTexts(header).ShouldContain(AgentPanelCopy.NoPlugins, StringComparer.Ordinal);
            NoticeButton(header, "ShowPipelineButton").IsEffectivelyVisible.ShouldBeTrue();
            HeaderButton(header, "CloseButton").IsEffectivelyVisible.ShouldBeTrue("the agent stays usable");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The tab order is Resume, task chip, Close, the trailing control, then the notice buttons.</summary>
    [Fact]
    public async Task TheTabOrderPlacesTheTrailingControlAfterClose()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var clock = new ManualTimeProvider();
            var row = PanelFixtures.Row("s1", SessionLifecycle.Lost, clock);
            using var model = PanelFixtures.Session(row, clock, context: ScriptedContextSource.Returning(PanelFixtures.Context()), resume: static _ => Task.CompletedTask);
            await model.ActivateAsync().ConfigureAwait(true);
            var toggle = new ToggleButton { Content = "Trailing control", MinHeight = 44 };
            AutomationProperties.SetName(toggle, "Trailing control");
            var header = Header(model, out var window, trailing: toggle);

            ViewHarness.TabOrder(header).ShouldBe(["Resume agent",
                "Task Ship the panel. Open task context.",
                "Close this agent",
                "Trailing control",
                "Resume agent",]);
            ViewHarness.Unnamed(header).ShouldBeEmpty();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A null data context draws an inert header; a swap redraws; a detached header stops following its model.</summary>
    [Fact]
    public async Task ADataContextSwapRedrawsAndDetachingStopsListening()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            using var first = PanelFixtures.Session(PanelFixtures.Row("a", SessionLifecycle.Running, clock, title: "First agent"), clock);
            using var second = PanelFixtures.Session(PanelFixtures.Row("b", SessionLifecycle.Created, clock, title: "Second agent"), clock);
            var header = Header(null, out var window);

            HeaderButton(header, "CloseButton").IsVisible.ShouldBeFalse();
            header.FindControl<Border>("SteeringBadge")!.IsVisible.ShouldBeFalse();
            ViewHarness.Unnamed(header).ShouldBeEmpty();

            header.DataContext = first;
            ViewHarness.Settle(window);
            ViewHarness.VisibleTexts(header).ShouldContain("First agent", StringComparer.Ordinal);
            header.DataContext = second;
            ViewHarness.Settle(window);
            ViewHarness.VisibleTexts(header).ShouldContain("Second agent", StringComparer.Ordinal);
            ViewHarness.VisibleTexts(header).ShouldNotContain("First agent", StringComparer.Ordinal);

            window.Content = null;
            second.ApplyStageEvent(PanelFixtures.Stage(StageSource.Ultrathink, "plan", StageStatus.Skipped, clock.GetUtcNow(), sessionId: "b"));
            NoticeOf(header).FindControl<Button>("ShowPipelineButton")!.IsVisible.ShouldBeFalse("a detached notice does not redraw");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Under reduced motion the header buttons keep no transition and the Running glyph is still; under full motion the Button background transitions.</summary>
    [Fact]
    public async Task ReducedMotionRemovesTransitionsAndThePulse()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            using var model = PanelFixtures.Session(PanelFixtures.Row("a", SessionLifecycle.Running, clock), clock);

            var reduced = Header(model, out var reducedWindow, reducedMotion: true);
            (HeaderButton(reduced, "CloseButton").Transitions?.Count ?? 0).ShouldBe(0);
            reduced.FindControl<StatusChip>("StatusChipView")!.FindControl<StageIcon>("Icon")!.IsPulsing.ShouldBeFalse();
            reducedWindow.Close();

            var full = Header(model, out var fullWindow);
            HeaderButton(full, "CloseButton").Transitions!.OfType<BrushTransition>().Single().Duration.ShouldBe(TimeSpan.FromMilliseconds(100));
            full.FindControl<StatusChip>("StatusChipView")!.FindControl<StageIcon>("Icon")!.IsPulsing.ShouldBeTrue();
            fullWindow.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
