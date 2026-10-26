using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using OmpOS.Contracts.Agents;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentPanel.Views;

/// <summary>The pipeline strip and its announcement line (46-UI-SPEC, Pipeline Strip and Live Regions).</summary>
public sealed class PipelineStripViewTests
{
    private static StageTimelineModel Timeline(ManualTimeProvider clock) => new("s1", clock);

    private static PipelineStripView Open(StageTimelineModel? timeline, out Window window, bool reducedMotion = false, double width = 640)
    {
        var strip = new PipelineStripView { DataContext = timeline };
        window = ViewHarness.Show(strip, reducedMotion, width);
        return strip;
    }

    private static void Populate(StageTimelineModel timeline, ManualTimeProvider clock)
    {
        var now = clock.GetUtcNow();
        timeline.Apply(PanelFixtures.Stage(StageSource.Ultrathink, "plan", StageStatus.Succeeded, now, title: "Planned", detail: "8 nodes, 4 waves, 2 clarifications", sessionId: "s1"));
        timeline.Apply(PanelFixtures.Stage(StageSource.Swarm, "run", StageStatus.Running, now, title: "Subagents", sessionId: "s1"));
        timeline.Apply(PanelFixtures.Stage(StageSource.Substrate, "brief", StageStatus.Succeeded, now, title: "Brief", sessionId: "s1"));
    }

    /// <summary>The strip is one 44 px button named by the model, whose pills carry the model's texts in pipeline order.</summary>
    [Fact]
    public async Task TheStripIsOneNamedButtonOfPills()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            Populate(timeline, clock);
            var strip = Open(timeline, out var window);

            var button = ViewHarness.Find<Button>(strip, "PipelineStrip");
            AutomationProperties.GetName(button).ShouldBe(timeline.PipelineAccessibleName);
            AutomationProperties.GetName(button).ShouldStartWith(AgentPanelCopy.PipelineAccessibleNamePrefix);
            button.MinHeight.ShouldBeGreaterThanOrEqualTo(44);
            button.Bounds.Height.ShouldBeGreaterThanOrEqualTo(44);
            var pillTexts = ViewHarness.VisibleTexts(button);
            pillTexts.ShouldBe(timeline.Pills.Select(static pill => pill.Text));
            timeline.Pills.Count.ShouldBe(4);
            ViewHarness.TabOrder(strip).ShouldBe(new[] { timeline.PipelineAccessibleName });
            ViewHarness.Unnamed(strip).ShouldBeEmpty();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A source with no events reads as waiting, and an event updates its pill and the button's name.</summary>
    [Fact]
    public async Task PillsAndNameFollowTheTimeline()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            var strip = Open(timeline, out var window);
            var button = ViewHarness.Find<Button>(strip, "PipelineStrip");
            var before = ViewHarness.VisibleTexts(button).ToList();

            before.ShouldBe(timeline.Pills.Select(static pill => pill.Text));

            Populate(timeline, clock);
            ViewHarness.Settle(window);

            ViewHarness.VisibleTexts(button).ShouldBe(timeline.Pills.Select(static pill => pill.Text));
            ViewHarness.VisibleTexts(button).SequenceEqual(before, StringComparer.Ordinal).ShouldBeFalse("the pills changed with the events");
            AutomationProperties.GetName(button).ShouldBe(timeline.PipelineAccessibleName);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The Latest line is a polite named live region that shows the timeline's announcement, collapses when there is none, and follows each new one.</summary>
    [Fact]
    public async Task TheLatestLineIsAPoliteLiveRegionBoundToTheAnnouncement()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            var strip = Open(timeline, out var window);
            var line = ViewHarness.Find<TextBlock>(strip, "Announcement");

            line.IsVisible.ShouldBeFalse("the line collapses while there is nothing to say");
            AutomationProperties.GetName(line).ShouldBe("Pipeline announcements");
            AutomationProperties.GetLiveSetting(line).ShouldBe(AutomationLiveSetting.Polite);

            timeline.Announce("Swarm started 5 subagents.");
            ViewHarness.Settle(window);
            line.IsVisible.ShouldBeTrue();
            line.Text.ShouldBe("Latest: Swarm started 5 subagents.");

            timeline.Announce("Agent finished.");
            ViewHarness.Settle(window);
            line.Text.ShouldBe("Latest: Agent finished.");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A stage transition reaches the line through the model's own sentence, which the view never builds.</summary>
    [Fact]
    public async Task StageTransitionsAreAnnouncedWithTheModelsSentence()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            var strip = Open(timeline, out var window);

            timeline.Apply(PanelFixtures.Stage(StageSource.Ultrathink, "plan", StageStatus.Succeeded, clock.GetUtcNow(), title: "Planned", sessionId: "s1"));
            ViewHarness.Settle(window);

            timeline.StatusAnnouncement.ShouldNotBeNullOrWhiteSpace();
            ViewHarness.Find<TextBlock>(strip, "Announcement").Text.ShouldBe($"Latest: {timeline.StatusAnnouncement}");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Pressing the strip opens a flyout that shows the stage rows of every source, with the detail text and the run summary.</summary>
    [Fact]
    public async Task PressingTheStripOpensTheStageRowsFlyout()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            Populate(timeline, clock);
            var strip = Open(timeline, out var window);
            strip.RunSummary = "Finished. Ultrathink: planned.";
            var button = ViewHarness.Find<Button>(strip, "PipelineStrip");
            var flyout = (Flyout)button.Flyout!;
            flyout.IsOpen.ShouldBeFalse("the flyout is closed until the strip is pressed");

            ViewHarness.Click(window, button);

            flyout.IsOpen.ShouldBeTrue("pressing the strip opens the flyout");
            var details = strip.FindControl<PipelineTabView>("Details")!;
            details.DataContext.ShouldBeSameAs(timeline);
            details.RunSummary.ShouldBe("Finished. Ultrathink: planned.");
            var texts = ViewHarness.VisibleTexts(details);
            texts.ShouldContain("Planned", StringComparer.Ordinal);
            texts.ShouldContain("8 nodes, 4 waves, 2 clarifications", StringComparer.Ordinal);
            texts.ShouldContain("Subagents", StringComparer.Ordinal);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>ShowDetails opens the same flyout, so the panel notice can point at the stage rows without a second surface.</summary>
    [Fact]
    public async Task ShowDetailsOpensTheFlyout()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            Populate(timeline, clock);
            var strip = Open(timeline, out var window);

            strip.ShowDetails();

            ((Flyout)ViewHarness.Find<Button>(strip, "PipelineStrip").Flyout!).IsOpen.ShouldBeTrue();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>At a narrow width the pills wrap onto a second line and the strip grows instead of truncating.</summary>
    [Fact]
    public async Task PillsWrapAtNarrowWidths()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            Populate(timeline, clock);
            var wide = Open(timeline, out var wideWindow, width: 900);
            var wideHeight = ViewHarness.Find<Button>(wide, "PipelineStrip").Bounds.Height;
            wideWindow.Close();

            var narrow = Open(timeline, out var narrowWindow, width: 260);
            var narrowHeight = ViewHarness.Find<Button>(narrow, "PipelineStrip").Bounds.Height;

            narrowHeight.ShouldBeGreaterThan(wideHeight);
            narrowWindow.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Under reduced motion the strip keeps no transition and a Running pill does not pulse; under full motion both stay.</summary>
    [Fact]
    public async Task ReducedMotionRemovesTheTransitionAndThePulse()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var timeline = Timeline(clock);
            Populate(timeline, clock);

            var reduced = Open(timeline, out var reducedWindow, reducedMotion: true);
            (ViewHarness.Find<Button>(reduced, "PipelineStrip").Transitions?.Count ?? 0).ShouldBe(0);
            ViewHarness.PulsingIcons(reduced).ShouldBe(0);
            reducedWindow.Close();

            var full = Open(timeline, out var fullWindow);
            ViewHarness.Find<Button>(full, "PipelineStrip").Transitions!.OfType<BrushTransition>().ShouldHaveSingleItem();
            ViewHarness.PulsingIcons(full).ShouldBeGreaterThan(0);
            fullWindow.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A null model draws an inert strip, a swap redraws it, and a detached strip stops following its timeline.</summary>
    [Fact]
    public async Task ADataContextSwapRedrawsAndDetachingStopsListening()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var first = Timeline(clock);
            var second = Timeline(clock);
            var strip = Open(null, out var window);
            var line = ViewHarness.Find<TextBlock>(strip, "Announcement");

            line.IsVisible.ShouldBeFalse();
            ViewHarness.Unnamed(strip).ShouldBeEmpty();

            strip.DataContext = first;
            first.Announce("First says hello.");
            ViewHarness.Settle(window);
            line.Text.ShouldBe("Latest: First says hello.");

            strip.DataContext = second;
            ViewHarness.Settle(window);
            line.IsVisible.ShouldBeFalse("the second timeline has said nothing");
            first.Announce("First again.");
            ViewHarness.Settle(window);
            line.IsVisible.ShouldBeFalse("the old timeline no longer drives the line");

            second.Announce("Second here.");
            ViewHarness.Settle(window);
            line.Text.ShouldBe("Latest: Second here.");

            window.Content = null;
            second.Announce("Nobody listens.");
            line.Text.ShouldBe("Latest: Second here.");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
