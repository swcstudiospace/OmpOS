using Bunit;
using Microsoft.AspNetCore.Components.Web;
using OmpOS.Contracts.Agents;
using OmpOS.Presentation.AgentPanel;
using OmpOS.UI.Components;
using OmpOS.UI.Components.Thread;
using OmpOS.UI.Tests.Fakes;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

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
            var strip = cut.Find("button.ompos-pipeline-strip");
            strip.GetAttribute("aria-label").ShouldBe(session.Timeline.PipelineAccessibleName);
            strip.GetAttribute("aria-expanded").ShouldBe("false");
            var pills = cut.FindAll(".ompos-pill");
            pills.Select(static pill => pill.QuerySelector(".ompos-pill-text")!.TextContent).ShouldBe(session.Timeline.Pills.Select(static pill => pill.Text).ToArray());
            pills.ShouldAllBe(static pill => pill.QuerySelector("svg")!.GetAttribute("aria-hidden") == "true");
        }).ConfigureAwait(true);
    }

    /// <summary>Opening the strip shows a named details region with a block per plugin; Escape closes it and returns the focus to the strip.</summary>
    [Fact]
    public async Task TheDetailsOpenAndEscapeReturnsTheFocus()
    {
        var (harness, session, cut) = await OpenAsync().ConfigureAwait(true);
        using var owned = harness;
        var stripId = cut.Find("button.ompos-pipeline-strip").InitialReferenceId();
        session.ApplyStageEvent(PanelEvents.Stage(StageSource.Ultrathink, "plan", StageStatus.Succeeded, "8 nodes"));

        await cut.Find("button.ompos-pipeline-strip").ClickAsync().ConfigureAwait(true);

        cut.Find("button.ompos-pipeline-strip").GetAttribute("aria-expanded").ShouldBe("true");
        var details = cut.Find(".ompos-pipeline-details");
        details.GetAttribute("role").ShouldBe("region");
        details.GetAttribute("aria-label").ShouldBe("Pipeline details");
        details.Id.ShouldBe(cut.Find("button.ompos-pipeline-strip").GetAttribute("aria-controls"));
        cut.FindAll(".ompos-stage-block").Count.ShouldBe(session.Timeline.Blocks.Count);
        cut.Find(".ompos-stage-row .ompos-stage-title").TextContent.ShouldBe("plan");
        cut.Find(".ompos-stage-row .ompos-stage-status").TextContent.ShouldBe("Done");

        await cut.Find(".ompos-pipeline").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }).ConfigureAwait(true);

        cut.FindAll(".ompos-pipeline-details").ShouldBeEmpty();
        JSInterop.FocusCalls()[^1].ReferenceIdOf().ShouldBe(stripId);
    }

    /// <summary>A plugin that is not installed shows its fix text as code, and Retry stage asks the host to check it again.</summary>
    [Fact]
    public async Task AnUnavailablePluginShowsTheFixAndRetriesTheCheck()
    {
        var (harness, session, cut) = await OpenAsync().ConfigureAwait(true);
        using var owned = harness;
        session.ApplyStageEvent(PanelEvents.Stage(StageSource.Ultrathink, "plugin", StageStatus.Unavailable, "Run: git submodule update --init plugins/claude-ultrathink"));
        await cut.Find("button.ompos-pipeline-strip").ClickAsync().ConfigureAwait(true);

        cut.Find("code.ompos-stage-fix").TextContent.ShouldContain("git submodule update", Case.Sensitive);
        var retry = cut.FindAll(".ompos-stage-row button").Single();
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
        cut.Find(".ompos-pipeline-latest").TextContent.ShouldBeEmpty();

        session.Timeline.Announce("Swarm: 2 of 5 done.");
        session.Timeline.FlushPending();

        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-pipeline-latest").TextContent.ShouldBe(AgentPanelViewText.LatestLine(session.Timeline.StatusAnnouncement!))).ConfigureAwait(true);
        var latest = cut.Find(".ompos-pipeline-latest");
        latest.GetAttribute("role").ShouldBe("status");
        latest.GetAttribute("aria-live").ShouldBe("polite");
    }
}
