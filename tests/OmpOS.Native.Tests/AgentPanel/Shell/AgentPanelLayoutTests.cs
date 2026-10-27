using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>The two regions of the Agent Panel and the width rules of the UI spec (Layout at 1280 x 800, Layout at 960 x 640, Responsive rules).</summary>
public sealed class AgentPanelLayoutTests
{
    /// <summary>The default window shows the rail, the thread header, the plugin connection row under it, the transcript and the composer.</summary>
    [Fact]
    public async Task TheDefaultWindowShowsRailThreadPipelineStripAndComposer()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);

            ShellTree.NamedVisible<ListBox>(main, "AgentRows").ShouldNotBeNull("the rail lists the agents");
            ShellTree.NamedVisible<Button>(main, "NewAgentButton").ShouldNotBeNull("the rail offers New agent");
            main.FindControl<ContentControl>("ThreadHeaderHost")!.Content.ShouldBeOfType<ThreadHeaderView>().IsEffectivelyVisible.ShouldBeTrue();
            ShellHarness.Transcript(window).IsEffectivelyVisible.ShouldBeTrue();
            ShellTree.NamedVisible<Button>(main, "PipelineStrip").ShouldNotBeNull("the pipeline strip shows under the header");
            var header = main.FindControl<ContentControl>("ThreadHeaderHost")!;
            var strip = main.FindControl<ContentControl>("PipelineHost")!;
            strip.Bounds.Top.ShouldBe(header.Bounds.Bottom, 1, "the strip is the row directly under the header");
            Grid.GetRow(strip).ShouldBe(Grid.GetRow(header) + 1);
            Grid.GetRow(strip).ShouldBeLessThan(Grid.GetRow((Control)main.FindControl<DockPanel>("TranscriptRegion")!.Parent!), "the strip sits above the transcript");
            ShellHarness.Composer(window).IsEffectivelyVisible.ShouldBeTrue();
            ShellHarness.Prompt(window).IsEffectivelyVisible.ShouldBeTrue();

            main.FindControl<Border>("RailRegion")!.Bounds.Width.ShouldBe(280, 1, "the rail is OmpOS.Size.AgentRailWidth at 1200 px and above");
            main.FindControl<Border>("ThreadRegion")!.Bounds.Width.ShouldBe(1000, 1, "the thread gets the rest: 1000 px at 1280");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The default panel is the Cursor-style panel: nothing sits beside the rail and the thread, and no inspector control exists (DR-1).</summary>
    [Fact]
    public async Task TheDefaultPanelHasNoRegionBesideRailThreadAndComposer()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            var window = ShellHarness.Show(fixture, width: 1280, height: 800);
            var main = ShellHarness.MainViewOf(window);

            var grid = main.FindControl<Grid>("RegionGrid")!;
            grid.Children.Where(static child => ((Control)child).IsVisible).Select(static child => ((Control)child).Name).ShouldBe(["RailRegion", "ThreadRegion"], "the panel shows exactly the rail and the thread");
            grid.Children.Select(static child => ((Control)child).Name).ShouldBe(["RailRegion", "ThreadRegion", "KanbanHost"], "the Kanban host is the one more region, hidden until the Kanban tab shows");
            grid.ColumnDefinitions.Count.ShouldBe(2);
            main.FindControl<Border>("RailRegion")!.Bounds.Width.ShouldBe(280, 1);
            main.FindControl<Border>("ThreadRegion")!.Bounds.Width.ShouldBe(1000, 1, "the thread takes everything the rail leaves");

            ShellTree.NamedVisible<ListBox>(main, "AgentRows").ShouldNotBeNull("the rail is there");
            ShellTree.NamedVisible<Control>(main, "TranscriptHost").ShouldNotBeNull("the thread is there");
            ShellHarness.Composer(window).IsEffectivelyVisible.ShouldBeTrue("the composer is there");

            var named = main.GetVisualDescendants().OfType<Control>().Select(static control => control.Name).Where(static name => name is not null).ToList();
            named.ShouldNotContain(static name => name!.Contains("Inspector", StringComparison.Ordinal), "there is no inspector control");
            main.GetVisualDescendants().OfType<TabControl>().ShouldBeEmpty("there is no tabbed side panel");
            main.GetVisualDescendants().OfType<TabItem>().ShouldBeEmpty();
            ShellTree.Named<Control>(main, "ContextTabHost").ShouldBeNull();
            ShellTree.Named<Control>(main, "SteeringTabHost").ShouldBeNull();
            ShellTree.Named<Control>(main, "PipelineTabHost").ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The two regions carry landmark names.</summary>
    [Fact]
    public async Task TheRegionsAreNamedLandmarks()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);

            AutomationProperties.GetName(main.FindControl<Border>("RailRegion")!).ShouldBe("Agents rail");
            AutomationProperties.GetName(main.FindControl<Border>("ThreadRegion")!).ShouldBe("Agent thread");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>At 960 px the rail is 240 px wide; the thread keeps its 400 px minimum, the strip and the composer stay.</summary>
    [Fact]
    public async Task TheNarrowWindowUsesTheNarrowRail()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            var window = ShellHarness.Show(fixture, width: 960, height: 640);
            var main = ShellHarness.MainViewOf(window);

            main.FindControl<Border>("RailRegion")!.Bounds.Width.ShouldBe(240, 1, "below 1200 px the rail is OmpOS.Size.SessionListWidth");
            main.FindControl<Border>("ThreadRegion")!.Bounds.Width.ShouldBe(720, 1, "the thread gets the rest at 960 px");
            main.FindControl<Border>("ThreadRegion")!.Bounds.Width.ShouldBeGreaterThanOrEqualTo(400, "the thread keeps OmpOS.Size.ThreadMinWidth at 960 px");
            ShellTree.NamedVisible<Button>(main, "PipelineStrip").ShouldNotBeNull("the pipeline strip stays in the narrow layout");
            ShellHarness.Prompt(window).IsEffectivelyVisible.ShouldBeTrue("the composer stays in the narrow layout");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The rail is 240 px below 1200 px and 280 px from 1200 px up, in both directions of a resize.</summary>
    [Fact]
    public async Task CrossingTheWideThresholdSwitchesTheRailWidthBothWays()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            var window = ShellHarness.Show(fixture, width: 1000, height: 700);
            var rail = ShellHarness.MainViewOf(window).FindControl<Border>("RailRegion")!;

            rail.Bounds.Width.ShouldBe(240, 1);
            fixture.Resize(1100, 700);
            rail.Bounds.Width.ShouldBe(240, 1, "the rail stays narrow while the width stays under 1200 px");

            fixture.Resize(1280, 800);
            rail.Bounds.Width.ShouldBe(280, 1, "from 1200 px the rail is wide");
            fixture.Resize(1300, 800);
            rail.Bounds.Width.ShouldBe(280, 1, "the rail stays wide while the width stays at or above 1200 px");

            fixture.Resize(900, 700);
            rail.Bounds.Width.ShouldBe(240, 1, "under 1200 px the rail is narrow again");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// The composer hint is a tooltip at every height, so the short-window rule is gone and nothing else changes below 720 px
    /// (cursor-agents-window-parity.md 4.1 and 4.7).
    /// </summary>
    [Fact]
    public async Task TheComposerHintIsATooltipInATallAndAShortWindow()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            var window = ShellHarness.Show(fixture, width: 1280, height: 800);

            ShellHarness.Composer(window).HintVisible.ShouldBeFalse("the hint is a tooltip, not a line");

            fixture.Resize(1280, 640);

            ShellHarness.Composer(window).HintVisible.ShouldBeFalse("below 720 px nothing changes");
            ShellHarness.Prompt(window).IsEffectivelyVisible.ShouldBeTrue("the prompt stays");
            ShellHarness.Send(window).IsEffectivelyVisible.ShouldBeTrue("the send control stays");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The motion setting reaches the main view, so its styles can remove transitions.</summary>
    [Fact]
    public async Task TheMotionSettingReachesTheMainView()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show(MotionPreference.Reduced);

            ShellHarness.MainViewOf(window).Classes.Contains(MainView.ReducedMotionClass).ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
