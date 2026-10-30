using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.UI.Components;
using Autonogrammer.UI.Components.Thread;
using Autonogrammer.UI.Tests.Fakes;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>The pipeline strip shows one pill per plugin with its status in words; its details list every stage with the fix text and Retry stage (PX-09).</summary>
public sealed class PipelineStripTests : UiTestContext
{
    private async Task<(PanelHarness Harness, AgentSessionViewModel Session, IRenderedComponent<PipelineStrip> Cut)> OpenAsync()
    {
        var harness = PanelHarness.With(("a", SessionLifecycle.Running, "Alpha"));
        var session = await harness.SelectAsync("a").ConfigureAwait(true);
        var cut = Render<PipelineStrip>(parameters => parameters.Add(p => p.Session, session));
        return (harness, session, cut);
    }

    /// <summary>The strip is one button named by the pipeline, with one decorative-glyph pill per plugin and the pill text visible.</summary>
    [Fact]
    public async Task TheStripShowsOnePillPerPlugin()
    {
        var (harness, session, cut) = await OpenAsync().ConfigureAwait(true);
        using var owned = harness;
        session.ApplyStageEvent(PanelEvents.Stage(StageSource.Ultrathink, "plan", StageStatus.Succeeded, "8 nodes"));
        session.ApplyStageEvent(PanelEvents.Stage(StageSource.Swarm, "wave", StageStatus.Running));

        await cut.WaitForAssertionAsync(() =>
        {
            var strip = cut.Find("button.autonogrammer-pipeline-strip");
            strip.GetAttribute("aria-label").ShouldBe(session.Timeline.PipelineAccessibleName);
            strip.GetAttribute("aria-expanded").ShouldBe("false");
            var pills = cut.FindAll(".autonogrammer-pill");
            pills.Select(static pill => pill.QuerySelector(".autonogrammer-pill-text")!.TextContent).ShouldBe(session.Timeline.Pills.Select(static pill => pill.Text).ToArray());
            pills.ShouldAllBe(static pill => pill.QuerySelector("svg")!.GetAttribute("aria-hidden") == "true");
        }).ConfigureAwait(true);
    }

    /// <summary>Opening the strip shows a named details region with a block per plugin; Escape closes it and returns the focus to the strip.</summary>
    [Fact]
    public async Task TheDetailsOpenAndEscapeReturnsTheFocus()
    {
        var (harness, session, cut) = await OpenAsync().ConfigureAwait(true);
        using var owned = harness;
        var stripId = cut.Find("button.autonogrammer-pipeline-strip").InitialReferenceId();
        session.ApplyStageEvent(PanelEvents.Stage(StageSource.Ultrathink, "plan", StageStatus.Succeeded, "8 nodes"));

        await cut.Find("button.autonogrammer-pipeline-strip").ClickAsync().ConfigureAwait(true);

        cut.Find("button.autonogrammer-pipeline-strip").GetAttribute("aria-expanded").ShouldBe("true");
        var details = cut.Find(".autonogrammer-pipeline-details");
        details.GetAttribute("role").ShouldBe("region");
        details.GetAttribute("aria-label").ShouldBe("Pipeline details");
        details.Id.ShouldBe(cut.Find("button.autonogrammer-pipeline-strip").GetAttribute("aria-controls"));
        cut.FindAll(".autonogrammer-stage-block").Count.ShouldBe(session.Timeline.Blocks.Count);
        cut.Find(".autonogrammer-stage-row .autonogrammer-stage-title").TextContent.ShouldBe("plan");
        cut.Find(".autonogrammer-stage-row .autonogrammer-stage-status").TextContent.ShouldBe("Done");

        await cut.Find(".autonogrammer-pipeline").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }).ConfigureAwait(true);

        cut.FindAll(".autonogrammer-pipeline-details").ShouldBeEmpty();
        JSInterop.FocusCalls()[^1].ReferenceIdOf().ShouldBe(stripId);
    }

    /// <summary>A plugin that is not installed shows its fix text as code, and Retry stage asks the host to check it again.</summary>
    [Fact]
    public async Task AnUnavailablePluginShowsTheFixAndRetriesTheCheck()
    {
        var (harness, session, cut) = await OpenAsync().ConfigureAwait(true);
        using var owned = harness;
        session.ApplyStageEvent(PanelEvents.Stage(StageSource.Ultrathink, "plugin", StageStatus.Unavailable, "Run: git submodule update --init plugins/claude-ultrathink"));
        await cut.Find("button.autonogrammer-pipeline-strip").ClickAsync().ConfigureAwait(true);

        cut.Find("code.autonogrammer-stage-fix").TextContent.ShouldContain("git submodule update", Case.Sensitive);
        var retry = cut.FindAll(".autonogrammer-stage-row button").Single();
        retry.TextContent.ShouldBe(AgentPanelCopy.RetryStage);
        retry.GetAttribute("aria-label").ShouldBe("Retry plugin stage");

        await retry.ClickAsync().ConfigureAwait(true);

        harness.Host.Rechecks.ShouldBe([StageSource.Ultrathink]);
    }

    /// <summary>The latest announcement of the timeline is a polite status line next to the strip.</summary>
    [Fact]
    public async Task TheLatestAnnouncementIsAPoliteStatus()
    {
        var (harness, session, cut) = await OpenAsync().ConfigureAwait(true);
        using var owned = harness;
        cut.Find(".autonogrammer-pipeline-latest").TextContent.ShouldBeEmpty();

        session.Timeline.Announce("Swarm: 2 of 5 done.");
        session.Timeline.FlushPending();

        await cut.WaitForAssertionAsync(() => cut.Find(".autonogrammer-pipeline-latest").TextContent.ShouldBe(AgentPanelViewText.LatestLine(session.Timeline.StatusAnnouncement!))).ConfigureAwait(true);
        var latest = cut.Find(".autonogrammer-pipeline-latest");
        latest.GetAttribute("role").ShouldBe("status");
        latest.GetAttribute("aria-live").ShouldBe("polite");
    }
}
