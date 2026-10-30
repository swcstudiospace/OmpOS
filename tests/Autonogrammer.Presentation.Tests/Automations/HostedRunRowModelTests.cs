using Autonogrammer.Presentation.Automations;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Automations;

/// <summary>One run of the history: its status words, when it started, how long it took, what it did and the agent it started.</summary>
public sealed class HostedRunRowModelTests
{
    /// <summary>The duration reads in hours, minutes and seconds, leaves out zero parts, and rounds down to whole seconds.</summary>
    [Theory]
    [InlineData(125, "took 2 min 5 s")]
    [InlineData(120, "took 2 min")]
    [InlineData(5, "took 5 s")]
    [InlineData(0, "took 0 s")]
    [InlineData(59.9, "took 59 s")]
    [InlineData(3725, "took 1 h 2 min 5 s")]
    [InlineData(3600, "took 1 h")]
    [InlineData(-4, "took 0 s")]
    public void DurationReadsInWholeUnits(double seconds, string expected)
    {
        var run = AutomationFixtures.Run("run-1", HostedRunStatus.Succeeded, duration: TimeSpan.FromSeconds(seconds));

        new HostedRunRowModel(run).DurationText.ShouldBe(expected);
    }

    /// <summary>A run in progress says running whether or not a duration is known; a finished run without a duration says nothing.</summary>
    [Fact]
    public void RunningRunSaysRunningAndUnknownDurationSaysNothing()
    {
        var running = AutomationFixtures.Run("run-1", HostedRunStatus.Running, duration: TimeSpan.FromSeconds(3));
        var unknown = AutomationFixtures.Run("run-2", HostedRunStatus.Failed, duration: null);

        new HostedRunRowModel(running).DurationText.ShouldBe("running");
        new HostedRunRowModel(unknown).DurationText.ShouldBeEmpty();
    }

    /// <summary>Each status has its own capitalised word.</summary>
    [Theory]
    [InlineData(HostedRunStatus.Running, "Running")]
    [InlineData(HostedRunStatus.Succeeded, "Succeeded")]
    [InlineData(HostedRunStatus.Launched, "Launched")]
    [InlineData(HostedRunStatus.Failed, "Failed")]
    [InlineData(HostedRunStatus.TimedOut, "Timed out")]
    [InlineData(HostedRunStatus.Cancelled, "Cancelled")]
    [InlineData(HostedRunStatus.Skipped, "Skipped")]
    public void EveryStatusHasItsOwnWord(HostedRunStatus status, string expected)
    {
        var row = new HostedRunRowModel(AutomationFixtures.Run("run-1", status));

        row.Status.ShouldBe(status);
        row.StatusText.ShouldBe(expected);
    }

    /// <summary>What started the run reads as a sentence, and an unknown trigger is named rather than hidden.</summary>
    [Theory]
    [InlineData("cron", "Started by the schedule")]
    [InlineData("webhook", "Started by a webhook")]
    [InlineData("manual", "Started by hand")]
    [InlineData("api", "Started by api")]
    public void TriggerReadsAsASentence(string trigger, string expected)
    {
        new HostedRunRowModel(AutomationFixtures.Run("run-1", HostedRunStatus.Succeeded, trigger)).TriggerText.ShouldBe(expected);
    }

    /// <summary>The start time is shown to the second in UTC, whatever offset it came with.</summary>
    [Fact]
    public void StartTimeIsShownInUtc()
    {
        var entry = AutomationFixtures.Run("run-1", HostedRunStatus.Succeeded) with
        {
            StartedAt = new DateTimeOffset(2026, 10, 28, 11, 0, 5, TimeSpan.FromHours(2)),
        };

        new HostedRunRowModel(entry).WhenText.ShouldBe("2026-10-28 09:00:05 UTC");
    }

    /// <summary>The summary, the error and the agent show when present; a run without a summary says so and one without an agent shows none.</summary>
    [Fact]
    public void SummaryErrorAndAgentShowWhenPresent()
    {
        var full = new HostedRunRowModel(AutomationFixtures.Run(
            "run-1",
            HostedRunStatus.Failed,
            summary: "Opened a pull request",
            error: "push rejected",
            externalId: "agent-9"));
        var bare = new HostedRunRowModel(AutomationFixtures.Run("run-2", HostedRunStatus.Succeeded));

        full.SummaryText.ShouldBe("Opened a pull request");
        full.ErrorText.ShouldBe("push rejected");
        full.AgentText.ShouldBe("Agent: agent-9");
        bare.SummaryText.ShouldBe(AutomationsCopy.NoSummary);
        bare.ErrorText.ShouldBeEmpty();
        bare.AgentText.ShouldBeEmpty();
    }

    /// <summary>The accessible name gives the status, the start time and, when known, the duration, so a screen reader needs nothing else.</summary>
    [Fact]
    public void AccessibleNameGivesStatusTimeAndDuration()
    {
        var finished = new HostedRunRowModel(AutomationFixtures.Run("run-1", HostedRunStatus.Succeeded, duration: TimeSpan.FromSeconds(65)));
        var unknown = new HostedRunRowModel(AutomationFixtures.Run("run-2", HostedRunStatus.Failed));

        finished.AccessibleName.ShouldBe("Succeeded, 2026-10-28 09:00:05 UTC, took 1 min 5 s, Started by the schedule");
        unknown.AccessibleName.ShouldBe("Failed, 2026-10-28 09:00:05 UTC, Started by the schedule");
    }
}
