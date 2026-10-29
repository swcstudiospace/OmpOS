using Autonogrammer.Contracts.Agents;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.AgentPanel;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Views;

/// <summary>The Pipeline tab: source blocks, rows, effect lines, fix text, Retry stage and the run summary (46-UI-SPEC, Pipeline Tab).</summary>
public sealed class PipelineTabViewTests
{
    private const string SwarmFix = "Swarm is not set up. plugins/agent-swarm is empty. Run: git submodule update --init --recursive plugins/agent-swarm";

    private static StageTimelineModel Timeline(ManualTimeProvider clock, Func<StageSource, CancellationToken, Task>? recheck = null) => new("s1", clock, recheck);

    private static PipelineTabView Open(StageTimelineModel? timeline, out Window window, bool reducedMotion = false)
    {
        var tab = new PipelineTabView { DataContext = timeline };
        window = ViewHarness.Show(tab, reducedMotion, height: 900);
        return tab;
    }

    private static StageEvent Event(ManualTimeProvider clock, StageSource source, string id, StageStatus status, string? title = null, string? detail = null, string? parent = null, string? graph = null) =>
        PanelFixtures.Stage(source, id, status, clock.GetUtcNow(), title, detail, parent, graph, "s1");

    private static Grid RowGrid(Control tab, StageRow row) =>
        tab.GetVisualDescendants().OfType<Grid>().First(grid => string.Equals(AutomationProperties.GetName(grid), row.AccessibleName, StringComparison.Ordinal));

    private static StageRow RowOf(StageTimelineModel timeline, string stageId) =>
        timeline.Blocks.SelectMany(static block => block.Rows).First(row => string.Equals(row.StageId, stageId, StringComparison.Ordinal));

    /// <summary>Before any event every source shows its block with the waiting line, in pipeline order.</summary>
    [Fact]
    public async Task LoadingStateRendersItsCopy()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            var tab = Open(timeline, out var window);

            var texts = ViewHarness.VisibleTexts(tab);
            timeline.Blocks.Count.ShouldBe(5);
            foreach (var block in timeline.Blocks)
            {
                texts.ShouldContain(block.Name, StringComparer.Ordinal);
            }

            texts.Count(static text => string.Equals(text, AgentPanelCopy.WaitingForEvents, StringComparison.Ordinal)).ShouldBe(5);
            texts.ShouldNotContain("Run summary", StringComparer.Ordinal);
            var order = timeline.Blocks.Select(static block => block.Name).ToList();
            order.ShouldBe(["Engine", "Ultrathink", "Swarm", "Substrate", "GSD"]);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Ultrathink shows its row with the observer's detail, and the Graph ID in a selectable mono line that is not a tab stop.</summary>
    [Fact]
    public async Task UltrathinkRowsShowDetailAndSelectableGraphId()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            timeline.Apply(Event(clock, StageSource.Ultrathink, "plan", StageStatus.Succeeded, "Planned", "8 nodes, 4 waves, 2 clarifications", graph: "ut-mvp0bh59-2dd6ef4c"));
            var tab = Open(timeline, out var window);

            var texts = ViewHarness.VisibleTexts(tab);
            texts.ShouldContain("Planned", StringComparer.Ordinal);
            texts.ShouldContain("8 nodes, 4 waves, 2 clarifications", StringComparer.Ordinal);
            texts.ShouldContain("Done", StringComparer.Ordinal);
            var graph = tab.GetVisualDescendants().OfType<SelectableTextBlock>().Single(static block => string.Equals(block.Text, "ut-mvp0bh59-2dd6ef4c", StringComparison.Ordinal));
            graph.IsTabStop.ShouldBeFalse();
            graph.FontFamily.ShouldBe(ViewHarness.MonoFont());
            ViewHarness.TabOrder(tab).ShouldBe(["Pipeline details"]);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Status words are coloured by role, and a row is named by its title, status and detail.</summary>
    [Fact]
    public async Task StatusWordsCarryTheirRoleAndRowsAreNamed()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            timeline.Apply(Event(clock, StageSource.Ultrathink, "plan", StageStatus.Succeeded, "Planned"));
            timeline.Apply(Event(clock, StageSource.Substrate, "brief", StageStatus.Failed, "Brief", "Brief refused"));
            timeline.Apply(Event(clock, StageSource.Gsd, "phase", StageStatus.Degraded, "Phase"));
            var tab = Open(timeline, out var window);

            var tones = tab.GetVisualDescendants().OfType<ToneTextBlock>().ToDictionary(static block => block.Text!, static block => block.Classes.ToList(), StringComparer.Ordinal);
            tones["Done"].ShouldContain("tone-success", StringComparer.Ordinal);
            tones["Failed"].ShouldContain("tone-error", StringComparer.Ordinal);
            tones["Degraded"].ShouldContain("tone-warning", StringComparer.Ordinal);
            AutomationProperties.GetName(RowGrid(tab, RowOf(timeline, "brief"))).ShouldBe("Brief, Failed, Brief refused");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Swarm children are indented one level under their parent and show their own status and detail.</summary>
    [Fact]
    public async Task SwarmChildrenAreIndentedUnderTheirParent()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            timeline.Apply(Event(clock, StageSource.Swarm, "subagents", StageStatus.Running, "Subagents", "3 running, 1 queued, 1 done"));
            timeline.Apply(Event(clock, StageSource.Swarm, "a05", StageStatus.Running, "a05-backend", "running \u00B7 42 s", parent: "subagents"));
            var tab = Open(timeline, out var window);

            var parent = RowGrid(tab, RowOf(timeline, "subagents"));
            var child = RowGrid(tab, RowOf(timeline, "a05"));
            parent.Margin.Left.ShouldBe(0);
            child.Margin.Left.ShouldBe(16);
            ViewHarness.VisibleTexts(tab).ShouldContain("running \u00B7 42 s", StringComparer.Ordinal);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Each degraded mode draws the effect line of the copy table under its row.</summary>
    [Theory]
    [InlineData(StageSource.Ultrathink, StageStatus.Skipped)]
    [InlineData(StageSource.Ultrathink, StageStatus.Degraded)]
    [InlineData(StageSource.Swarm, StageStatus.Unavailable)]
    [InlineData(StageSource.Substrate, StageStatus.Unavailable)]
    [InlineData(StageSource.Gsd, StageStatus.Failed)]
    public async Task DegradedRowsDrawTheirEffectLine(StageSource source, StageStatus status)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            timeline.Apply(Event(clock, source, "stage", status, "The stage"));
            var tab = Open(timeline, out var window);

            var effect = AgentPanelCopy.StageEffect(source, status);
            effect.ShouldNotBeNull();
            ViewHarness.VisibleTexts(tab).ShouldContain(effect, StringComparer.Ordinal);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An uninitialised submodule shows the exact fix text from the observer, selectable and in the mono face.</summary>
    [Fact]
    public async Task UninitialisedSubmoduleShowsFix()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            timeline.Apply(Event(clock, StageSource.Swarm, "swarm", StageStatus.Unavailable, "Swarm", SwarmFix));
            var tab = Open(timeline, out var window);

            var fix = tab.GetVisualDescendants().OfType<SelectableTextBlock>().Single(static block => block.IsEffectivelyVisible);
            fix.Text.ShouldBe(SwarmFix);
            fix.IsTabStop.ShouldBeFalse();
            fix.FontFamily.ShouldBe(ViewHarness.MonoFont());
            ViewHarness.VisibleTexts(tab).Count(static text => string.Equals(text, SwarmFix, StringComparison.Ordinal)).ShouldBe(1, "the fix is drawn once, in the selectable line");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A detail longer than the model's cap is drawn as the model cut it, ending in an ellipsis.</summary>
    [Fact]
    public async Task LongTextIsCapped()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            timeline.Apply(Event(clock, StageSource.Ultrathink, "plan", StageStatus.Succeeded, "Planned", new string('x', 500)));
            var tab = Open(timeline, out var window);

            var detail = RowOf(timeline, "plan").Detail!;
            detail.Length.ShouldBeLessThanOrEqualTo(StageTimelineModel.MaxDetailChars + 1);
            detail.ShouldEndWith("\u2026");
            ViewHarness.VisibleTexts(tab).ShouldContain(detail, StringComparer.Ordinal);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Retry stage appears only on failed, degraded and unavailable rows, is named for its stage, and re-checks only that source.</summary>
    [Fact]
    public async Task StageErrorDoesNotBlockThePanel()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var rechecked = new List<StageSource>();
            var timeline = Timeline(clock, (source, _) =>
            {
                rechecked.Add(source);
                return Task.CompletedTask;
            });
            timeline.Apply(Event(clock, StageSource.Ultrathink, "plan", StageStatus.Succeeded, "Planned"));
            timeline.Apply(Event(clock, StageSource.Substrate, "brief", StageStatus.Failed, "Brief", "Brief refused"));
            var tab = Open(timeline, out var window);

            var retries = tab.GetVisualDescendants().OfType<Button>().Where(static button => button.IsEffectivelyVisible).ToList();
            retries.Count.ShouldBe(1);
            var retry = retries.Single();
            retry.Content.ShouldBe(AgentPanelCopy.RetryStage);
            AutomationProperties.GetName(retry).ShouldBe("Retry Brief stage");
            retry.MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            ViewHarness.TabOrder(tab).ShouldBe(["Pipeline details", "Retry Brief stage"]);
            ViewHarness.Unnamed(tab).ShouldBeEmpty();

            ViewHarness.Click(window, retry);

            rechecked.ShouldBe(new[] { StageSource.Substrate });
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The run summary shows above the blocks when it is set and hides when it is cleared.</summary>
    [Fact]
    public async Task SuccessShowsStageSummary()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var tab = Open(Timeline(clock), out var window);
            const string summary = "Finished. Ultrathink: planned. Swarm: 5 of 5 done. Substrate: ok. GSD: 46, plan 2 of 5.";

            tab.RunSummary = summary;
            ViewHarness.Settle(window);
            ViewHarness.VisibleTexts(tab).ShouldContain("Run summary", StringComparer.Ordinal);
            ViewHarness.VisibleTexts(tab).ShouldContain(summary, StringComparer.Ordinal);

            tab.RunSummary = null;
            ViewHarness.Settle(window);
            ViewHarness.VisibleTexts(tab).ShouldNotContain("Run summary", StringComparer.Ordinal);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The scroller is the named, focusable stop of the tab.</summary>
    [Fact]
    public async Task TheScrollerIsTheNamedStop()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var tab = Open(Timeline(clock), out var window);
            var scroller = ViewHarness.Find<ScrollViewer>(tab, "PipelineDetails");

            AutomationProperties.GetName(scroller).ShouldBe("Pipeline details");
            scroller.Focusable.ShouldBeTrue();
            scroller.IsTabStop.ShouldBeTrue();
            ViewHarness.Unnamed(tab).ShouldBeEmpty();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Under reduced motion the Retry button keeps no transition and the Running glyph is still.</summary>
    [Fact]
    public async Task ReducedMotionRemovesTransitionsAndThePulse()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock, static (_, _) => Task.CompletedTask);
            timeline.Apply(Event(clock, StageSource.Swarm, "run", StageStatus.Running, "Subagents"));
            timeline.Apply(Event(clock, StageSource.Substrate, "brief", StageStatus.Failed, "Brief"));

            var reduced = Open(timeline, out var reducedWindow, reducedMotion: true);
            ViewHarness.PulsingIcons(reduced).ShouldBe(0);
            reduced.GetVisualDescendants().OfType<Button>().Where(static button => button.IsEffectivelyVisible)
                .ShouldAllBe(static button => (button.Transitions == null ? 0 : button.Transitions.Count) == 0);
            reducedWindow.Close();

            var full = Open(timeline, out var fullWindow);
            ViewHarness.PulsingIcons(full).ShouldBeGreaterThan(0);
            full.GetVisualDescendants().OfType<Button>().First(static button => button.IsEffectivelyVisible).Transitions!.OfType<BrushTransition>().ShouldHaveSingleItem();
            fullWindow.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A null model draws nothing and does not throw, and a swap replaces the blocks.</summary>
    [Fact]
    public async Task ADataContextSwapRedraws()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var first = Timeline(clock);
            first.Apply(Event(clock, StageSource.Ultrathink, "plan", StageStatus.Succeeded, "First plan"));
            var second = Timeline(clock);
            second.Apply(Event(clock, StageSource.Ultrathink, "plan", StageStatus.Succeeded, "Second plan"));
            var tab = Open(null, out var window);

            ViewHarness.VisibleTexts(tab).ShouldBeEmpty();
            ViewHarness.Unnamed(tab).ShouldBeEmpty();

            tab.DataContext = first;
            ViewHarness.Settle(window);
            ViewHarness.VisibleTexts(tab).ShouldContain("First plan", StringComparer.Ordinal);

            tab.DataContext = second;
            ViewHarness.Settle(window);
            ViewHarness.VisibleTexts(tab).ShouldContain("Second plan", StringComparer.Ordinal);
            ViewHarness.VisibleTexts(tab).ShouldNotContain("First plan", StringComparer.Ordinal);

            first.Apply(Event(clock, StageSource.Swarm, "late", StageStatus.Running, "Late row"));
            ViewHarness.Settle(window);
            ViewHarness.VisibleTexts(tab).ShouldNotContain("Late row", StringComparer.Ordinal);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
