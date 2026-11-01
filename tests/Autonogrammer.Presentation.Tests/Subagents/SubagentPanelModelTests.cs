using Autonogrammer.Presentation.Subagents;
using Autonogrammer.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Subagents;

/// <summary>The sub-agent panel over recorded task frames: names, fallback labels, status, running count and plain text.</summary>
public sealed class SubagentPanelModelTests
{
    /// <summary>Named frames produce one row per call, with status, elapsed time from the injected clock and the last plain-text line.</summary>
    [Fact]
    public async Task NamedFramesProduceRowsWithElapsedTime()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider();
        var model = new SubagentPanelModel(clock);
        Apply(model, RecordedTaskFrames.WithNames);
        clock.Advance(TimeSpan.FromSeconds(125));
        model.RefreshElapsed();
        await Task.Yield();
        token.ThrowIfCancellationRequested();

        model.LimitedToAcp.ShouldBeTrue();
        model.AcpOnlyNotice.ShouldBe(SubagentCopy.AcpOnlyNotice);
        model.Rows.Count.ShouldBe(2);
        model.Rows[0].Name.ShouldBe("researcher");
        model.Rows[0].Status.ShouldBe(SubagentStatus.Running);
        model.Rows[0].Elapsed.ShouldBe(TimeSpan.FromSeconds(125));
        model.Rows[0].LastLine.ShouldBe("Searching the repo.");
        model.Rows[0].IsPlainText.ShouldBeTrue();
        model.Rows[1].Name.ShouldBe("writer");
        model.Rows[1].LastLine.ShouldBe("Drafting the note.");
        model.RunningCount.ShouldBe(2);
        model.SubagentNamesUnavailable.ShouldBeFalse();
    }

    /// <summary>Frames without names read Sub-agent 1 and Sub-agent 2 and report SubagentNamesUnavailable.</summary>
    [Fact]
    public void UnnamedFramesUseNumberedLabels()
    {
        var model = new SubagentPanelModel(new ManualTimeProvider());
        Apply(model, RecordedTaskFrames.WithoutNames);

        model.SubagentNamesUnavailable.ShouldBeTrue();
        model.Rows.Select(row => row.Name).ShouldBe(["Sub-agent 1", "Sub-agent 2"]);
        model.Rows.ShouldAllBe(row => row.Name.Length > 0);
        model.Rows.ShouldAllBe(row => !row.NameFromCall);
    }

    /// <summary>Done, failed and cancelled updates move row status, and the running count tracks exactly.</summary>
    [Fact]
    public void StatusUpdatesMoveRowsAndTheRunningCount()
    {
        var model = new SubagentPanelModel(new ManualTimeProvider());
        Apply(model, RecordedTaskFrames.StatusTransitions);

        model.Rows.Single(row => string.Equals(row.CallId, "call-run", StringComparison.Ordinal)).Status.ShouldBe(SubagentStatus.Running);
        model.Rows.Single(row => string.Equals(row.CallId, "call-done", StringComparison.Ordinal)).Status.ShouldBe(SubagentStatus.Done);
        model.Rows.Single(row => string.Equals(row.CallId, "call-fail", StringComparison.Ordinal)).Status.ShouldBe(SubagentStatus.Failed);
        model.Rows.Single(row => string.Equals(row.CallId, "call-stop", StringComparison.Ordinal)).Status.ShouldBe(SubagentStatus.Cancelled);
        model.RunningCount.ShouldBe(1);
    }

    /// <summary>Markup and link text stay literal. No row carries markup as structure or an opening link.</summary>
    [Fact]
    public void AgentOutputStaysPlainText()
    {
        var model = new SubagentPanelModel(new ManualTimeProvider());
        Apply(model, RecordedTaskFrames.MarkupOutput);

        var row = model.Rows.ShouldHaveSingleItem();
        row.LastLine.ShouldBe("<a href=\"https://evil.example\">click</a> [open](https://evil.example)");
        row.IsPlainText.ShouldBeTrue();
        row.OpeningLink.ShouldBeNull();
        row.LastLine.ShouldContain("<a href=", Case.Sensitive);
        row.LastLine.ShouldContain("https://evil.example", Case.Sensitive);
    }

    private static void Apply(SubagentPanelModel model, IReadOnlyList<SubagentActivityUpdateEventArgs> frames)
    {
        foreach (var frame in frames)
        {
            model.Apply(frame);
        }
    }
}
