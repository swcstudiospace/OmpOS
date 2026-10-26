using OmpOS.Contracts.Agents;
using OmpOS.Presentation.AgentPanel;
using Shouldly;
using Xunit;
using static OmpOS.Presentation.Tests.AgentPanel.AgentPanelTestData;

namespace OmpOS.Presentation.Tests.AgentPanel;

/// <summary>
/// The polite <c>Pipeline announcements</c> sentence: one per stage transition, never per token, never repeated for an unchanged status,
/// and subagent transitions coalesced to one sentence per second (46-UI-SPEC, Live Regions).
/// </summary>
public sealed class StageTimelineAnnouncementTests
{
    private readonly ManualTimeProvider _clock = new();

    /// <summary>The first transition into Running is announced once, with the sentence of its source.</summary>
    [Fact]
    public void ATransitionIntoRunningIsAnnounced()
    {
        var model = NewModel();

        model.Apply(Stage(StageSource.Ultrathink, "plan", StageStatus.Running, Now()));

        model.StatusAnnouncement.ShouldBe("Ultrathink is planning the prompt.");
        model.AnnouncementVersion.ShouldBe(1);
    }

    /// <summary>Succeeded Ultrathink adds what the observer's detail says and nothing else.</summary>
    [Fact]
    public void AnUltrathinkSuccessAnnouncesThePlanDetail()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Ultrathink, "plan", StageStatus.Running, Now()));

        model.Apply(Stage(StageSource.Ultrathink, "plan", StageStatus.Succeeded, Now(), detail: "8 nodes, 4 waves, 2 clarifications"));

        model.StatusAnnouncement.ShouldBe("Ultrathink planned. 8 nodes, 4 waves, 2 clarifications.");
        model.AnnouncementVersion.ShouldBe(2);
    }

    /// <summary>A success without detail announces no invented counts.</summary>
    [Fact]
    public void AnUltrathinkSuccessWithoutDetailInventsNoCounts()
    {
        var model = NewModel();

        model.Apply(Stage(StageSource.Ultrathink, "plan", StageStatus.Succeeded, Now()));

        model.StatusAnnouncement.ShouldBe("Ultrathink planned.");
    }

    /// <summary>A repeated status, with a new detail or time, is not announced again.</summary>
    [Fact]
    public void AnUnchangedStatusIsNotAnnouncedAgain()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Running, Now(), detail: "1 running"));
        var version = model.AnnouncementVersion;
        var announced = PropertyChangeRecorder.Record(model);

        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Running, Now().AddSeconds(3), detail: "2 running"));
        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Running, Now().AddSeconds(6), detail: "3 running"));

        model.AnnouncementVersion.ShouldBe(version);
        announced.ShouldNotContain(nameof(StageTimelineModel.StatusAnnouncement), StringComparer.Ordinal);
    }

    /// <summary>A stage that goes back to a status it had before is a new transition and is announced.</summary>
    [Fact]
    public void ReturningToAnEarlierStatusIsANewTransition()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Substrate, "brief", StageStatus.Succeeded, Now()));
        model.Apply(Stage(StageSource.Substrate, "brief", StageStatus.Unavailable, Now().AddSeconds(1)));
        model.Apply(Stage(StageSource.Substrate, "brief", StageStatus.Succeeded, Now().AddSeconds(2)));

        model.AnnouncementVersion.ShouldBe(3);
        model.StatusAnnouncement.ShouldBe("Substrate is ok.");
    }

    /// <summary>Pending is a state, not progress: it is never announced.</summary>
    [Fact]
    public void PendingIsNeverAnnounced()
    {
        var model = NewModel();

        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Pending, Now()));

        model.StatusAnnouncement.ShouldBeNull();
        model.AnnouncementVersion.ShouldBe(0);
    }

    /// <summary>Degraded, failed, skipped and unavailable stages are polite warnings that say what they mean for the run.</summary>
    [Theory]
    [InlineData(StageSource.Substrate, StageStatus.Unavailable, "Substrate is not reachable. Steering is not recorded.")]
    [InlineData(StageSource.Swarm, StageStatus.Unavailable, "Swarm is not available. This agent runs alone.")]
    [InlineData(StageSource.Swarm, StageStatus.Failed, "Swarm stopped. Work continues as a single agent.")]
    [InlineData(StageSource.Ultrathink, StageStatus.Skipped, "Ultrathink is off. Prompts run as written.")]
    [InlineData(StageSource.Ultrathink, StageStatus.Degraded, "Ultrathink fell back to a plain spec.")]
    [InlineData(StageSource.Gsd, StageStatus.Unavailable, "GSD tools were not found.")]
    [InlineData(StageSource.Omp, StageStatus.Failed, "The engine failed.")]
    public void WarningsSaySourceAndEffect(StageSource source, StageStatus status, string expected)
    {
        var model = NewModel();

        model.Apply(Stage(source, "stage", status, Now()));

        model.StatusAnnouncement.ShouldBe(expected);
    }

    /// <summary>Stage detail text is never spoken for a warning: only the fixed sentence is.</summary>
    [Fact]
    public void WarningDetailIsNotSpoken()
    {
        var model = NewModel();

        model.Apply(Stage(StageSource.Substrate, "brief", StageStatus.Failed, Now(), detail: "token=abc123 refused"));

        model.StatusAnnouncement.ShouldNotBeNull().ShouldNotContain("abc123");
    }

    /// <summary>Several subagent transitions in one second give one sentence, and the next second carries the held ones.</summary>
    [Fact]
    public void SubagentTransitionsAreCoalescedToOnePerSecond()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Running, Now()));
        model.StatusAnnouncement.ShouldBe("Swarm started.");

        for (var index = 1; index <= 5; index++)
        {
            model.Apply(Stage(StageSource.Swarm, $"agent-{index}", StageStatus.Running, Now(), parent: "subagents"));
        }

        model.StatusAnnouncement.ShouldBe("1 subagent started.");
        model.AnnouncementVersion.ShouldBe(2);

        _clock.Advance(TimeSpan.FromSeconds(1));
        model.Apply(Stage(StageSource.Swarm, "agent-6", StageStatus.Running, Now(), parent: "subagents"));

        model.StatusAnnouncement.ShouldBe("5 subagents started.");
        model.AnnouncementVersion.ShouldBe(3);
    }

    /// <summary>A flush speaks the held subagent transitions at once, and nothing when none is held.</summary>
    [Fact]
    public void FlushSpeaksHeldSubagentTransitions()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Running, Now()));
        model.Apply(Stage(StageSource.Swarm, "agent-1", StageStatus.Running, Now(), parent: "subagents"));
        model.Apply(Stage(StageSource.Swarm, "agent-1", StageStatus.Succeeded, Now(), parent: "subagents"));
        model.Apply(Stage(StageSource.Swarm, "agent-2", StageStatus.Running, Now(), parent: "subagents"));
        model.Apply(Stage(StageSource.Swarm, "agent-2", StageStatus.Failed, Now(), parent: "subagents"));
        var version = model.AnnouncementVersion;

        model.FlushPending();

        model.StatusAnnouncement.ShouldBe("1 subagent started, 1 subagent finished, 1 subagent failed.");
        model.AnnouncementVersion.ShouldBe(version + 1);

        model.FlushPending();
        model.AnnouncementVersion.ShouldBe(version + 1);
    }

    /// <summary>A parent transition carries the held subagent sentence in front of its own.</summary>
    [Fact]
    public void AParentTransitionCarriesHeldSubagentTransitions()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Running, Now()));
        model.Apply(Stage(StageSource.Swarm, "agent-1", StageStatus.Running, Now(), parent: "subagents"));
        model.Apply(Stage(StageSource.Swarm, "agent-2", StageStatus.Running, Now(), parent: "subagents"));
        model.Apply(Stage(StageSource.Swarm, "agent-3", StageStatus.Running, Now(), parent: "subagents"));

        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Succeeded, Now().AddSeconds(1)));

        model.StatusAnnouncement.ShouldBe("2 subagents started. Swarm finished.");
    }

    /// <summary>A child that degrades or is skipped is not counted among starts, finishes or failures.</summary>
    [Fact]
    public void ChildTransitionsOtherThanStartFinishFailAreNotCounted()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Running, Now()));
        var version = model.AnnouncementVersion;

        model.Apply(Stage(StageSource.Swarm, "agent-1", StageStatus.Skipped, Now(), parent: "subagents"));
        model.Apply(Stage(StageSource.Swarm, "agent-2", StageStatus.Degraded, Now(), parent: "subagents"));
        model.FlushPending();

        model.AnnouncementVersion.ShouldBe(version);
    }

    /// <summary>An explicit announcement raises the version and the change even when the text repeats.</summary>
    [Fact]
    public void AnExplicitAnnouncementRepeatsEvenWithTheSameText()
    {
        var model = NewModel();
        var announced = PropertyChangeRecorder.Record(model);

        model.Announce("Agent finished.");
        model.Announce("Agent finished.");

        model.AnnouncementVersion.ShouldBe(2);
        announced.Count(name => string.Equals(name, nameof(StageTimelineModel.StatusAnnouncement), StringComparison.Ordinal)).ShouldBe(2);
    }

    /// <summary>A blank explicit announcement is refused.</summary>
    [Fact]
    public void ABlankAnnouncementIsRefused()
    {
        var model = NewModel();

        Should.Throw<ArgumentException>(() => model.Announce("  "));
    }

    private DateTimeOffset Now() => _clock.GetUtcNow();

    private StageTimelineModel NewModel() => new(SessionId, _clock);
}
