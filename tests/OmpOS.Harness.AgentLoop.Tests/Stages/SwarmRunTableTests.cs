using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Stages;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Stages;

/// <summary>
/// Verifies the swarm state vocabulary mapping and the run table: every state of the schema, the Task Store words, unknown
/// states, forward-only progress, finished runs staying finished, identifier and text hygiene, and the run cap.
/// </summary>
public sealed class SwarmRunTableTests
{
    private static SwarmEventRecord State(string runId, string to, string? agent = null) =>
        new() { Type = "swarm.run.state", Payload = new SwarmEventRecord.PayloadPart { RunId = runId, To = to, Agent = agent } };

    private static SwarmLiveDocument Live(params SwarmLiveDocument.RunPart[] runs) =>
        new() { Schema = SwarmLiveDocument.SchemaId, Runs = [.. runs] };

    private static StageSnapshot Child(SwarmRunTable table, string runId) =>
        table.Project(true, false, null).Single(snapshot => string.Equals(snapshot.StageId, "agent:" + runId, StringComparison.Ordinal));

    [Theory]
    [InlineData("waiting", StageStatus.Pending, "Waiting for dependencies")]
    [InlineData("queued", StageStatus.Pending, "Queued")]
    [InlineData("starting", StageStatus.Running, "Starting")]
    [InlineData("running", StageStatus.Running, "Running")]
    [InlineData("handing_off", StageStatus.Running, "Handing off")]
    [InlineData("completed", StageStatus.Succeeded, "Completed")]
    [InlineData("failed", StageStatus.Failed, "Failed")]
    [InlineData("cancelled", StageStatus.Skipped, "Cancelled")]
    [InlineData("timed_out", StageStatus.Failed, "Timed out")]
    public void EveryStateOfTheSchemaMapsToItsStageStatusAndLabel(string state, StageStatus status, string detail)
    {
        var table = new SwarmRunTable();

        table.ApplyLive(Live(new SwarmLiveDocument.RunPart { RunId = "T-1.a1", Agent = "a05-backend", State = state }));

        var child = Child(table, "T-1.a1");
        child.Status.ShouldBe(status);
        child.Detail.ShouldBe(detail);
        child.Title.ShouldBe("a05-backend");
        child.ParentStageId.ShouldBe("swarm");
    }

    [Theory]
    [InlineData("CLAIMED", StageStatus.Running)]
    [InlineData("IN_PROGRESS", StageStatus.Running)]
    [InlineData("PLANNED", StageStatus.Pending)]
    [InlineData("RETRY", StageStatus.Pending)]
    [InlineData("BLOCKED", StageStatus.Pending)]
    [InlineData("IN_REVIEW", StageStatus.Succeeded)]
    [InlineData("APPROVED", StageStatus.Succeeded)]
    [InlineData("DONE", StageStatus.Succeeded)]
    [InlineData("FAILED", StageStatus.Failed)]
    [InlineData("ESCALATED", StageStatus.Failed)]
    [InlineData("CANCELLED", StageStatus.Skipped)]
    public void TaskStoreWordsOfASubagentStatusStreamMapThroughTheSameTable(string word, StageStatus status)
    {
        var table = new SwarmRunTable();

        table.ApplyEvent(new SwarmEventRecord
        {
            Type = "subagent.status.stream",
            Payload = new SwarmEventRecord.PayloadPart { SubagentId = "gsd-1", Status = word, State = word },
        });

        Child(table, "gsd-1").Status.ShouldBe(status);
    }

    [Fact]
    public void UnknownStateIsRunningWithTheCleanedRawWordAsDetail()
    {
        var table = new SwarmRunTable();

        table.ApplyEvent(State("T-1.a1", "paus\u001bed\nfor-review"));

        var child = Child(table, "T-1.a1");
        child.Status.ShouldBe(StageStatus.Running);
        child.Detail.ShouldBe("paus ed for-review");
    }

    [Fact]
    public void ARunNeverMovesBackwardsSoALateEventCannotUndoTheLiveFile()
    {
        var table = new SwarmRunTable();
        table.ApplyLive(Live(new SwarmLiveDocument.RunPart { RunId = "r.a1", State = "handing_off" }));

        table.ApplyEvent(State("r.a1", "running"));
        table.ApplyEvent(State("r.a1", "queued"));

        Child(table, "r.a1").Detail.ShouldBe("Handing off");
    }

    [Fact]
    public void AFinishedRunStaysFinished()
    {
        var table = new SwarmRunTable();
        table.ApplyEvent(State("r.a1", "failed"));

        table.ApplyEvent(State("r.a1", "running"));
        table.ApplyLive(Live(new SwarmLiveDocument.RunPart { RunId = "r.a1", State = "completed" }));

        Child(table, "r.a1").Status.ShouldBe(StageStatus.Failed);
    }

    [Fact]
    public void ARetryIsANewRunBecauseTheAttemptIsPartOfTheRunId()
    {
        var table = new SwarmRunTable();
        table.ApplyEvent(State("T-1.a1", "failed"));
        table.ApplyEvent(State("T-1.a2", "running"));

        var snapshots = table.Project(true, false, null);

        snapshots.Single(s => string.Equals(s.StageId, "agent:T-1.a1", StringComparison.Ordinal)).Status.ShouldBe(StageStatus.Failed);
        snapshots.Single(s => string.Equals(s.StageId, "agent:T-1.a2", StringComparison.Ordinal)).Status.ShouldBe(StageStatus.Running);
        snapshots[0].Status.ShouldBe(StageStatus.Running);
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("has space")]
    [InlineData("")]
    [InlineData("-leading-hyphen")]
    public void RunIdentifiersThatAreNotPlainIdentifiersAreIgnored(string runId)
    {
        var table = new SwarmRunTable();

        table.ApplyEvent(State(runId, "running"));

        table.Count.ShouldBe(0);
    }

    [Fact]
    public void AgentTitleIsCleanedAndBounded()
    {
        var table = new SwarmRunTable();

        table.ApplyEvent(State("r.a1", "running", "a05\u001b[31m-backend\n" + new string('z', 200)));

        var title = Child(table, "r.a1").Title;
        title.Length.ShouldBeLessThanOrEqualTo(64);
        title.ShouldStartWith("a05 [31m-backend");
        title.ShouldNotContain("\u001b");
        title.ShouldNotContain("\n");
    }

    [Fact]
    public void OnlyAnErrorCodeIsShownNeverFreeErrorText()
    {
        var table = new SwarmRunTable();
        table.ApplyLive(Live(
            new SwarmLiveDocument.RunPart { RunId = "a.a1", State = "failed", Error = "E-TIMEOUT" },
            new SwarmLiveDocument.RunPart { RunId = "b.a1", State = "failed", Error = "token sk-abc leaked in /home/user/secret" }));

        Child(table, "a.a1").Detail.ShouldBe("Failed [E-TIMEOUT]");
        Child(table, "b.a1").Detail.ShouldBe("Failed");
    }

    [Fact]
    public void StalledRunningRunIsMarkedAndTheStalledCounterAppearsOnTheParent()
    {
        var table = new SwarmRunTable();
        table.ApplyLive(Live(new SwarmLiveDocument.RunPart { RunId = "r.a1", State = "running", Stalled = true }));

        var snapshots = table.Project(true, false, null);

        snapshots.Single(s => string.Equals(s.StageId, "agent:r.a1", StringComparison.Ordinal)).Detail.ShouldBe("Running (stalled)");
        snapshots[0].Detail.ShouldBe("1 running, 0 queued, 0 completed, 0 failed, 1 stalled");
    }

    [Fact]
    public void ParentCountersCoverEveryPhase()
    {
        var table = new SwarmRunTable();
        table.ApplyLive(Live(
            new SwarmLiveDocument.RunPart { RunId = "a.a1", State = "running" },
            new SwarmLiveDocument.RunPart { RunId = "b.a1", State = "handing_off" },
            new SwarmLiveDocument.RunPart { RunId = "c.a1", State = "queued" },
            new SwarmLiveDocument.RunPart { RunId = "d.a1", State = "waiting" },
            new SwarmLiveDocument.RunPart { RunId = "e.a1", State = "completed" },
            new SwarmLiveDocument.RunPart { RunId = "f.a1", State = "timed_out" },
            new SwarmLiveDocument.RunPart { RunId = "g.a1", State = "cancelled" }));

        var parent = table.Project(true, false, "ut-graph")[0];

        parent.StageId.ShouldBe("swarm");
        parent.Title.ShouldBe("Subagents");
        parent.Status.ShouldBe(StageStatus.Running);
        parent.Detail.ShouldBe("2 running, 1 queued, 1 completed, 1 failed, 1 waiting, 1 cancelled");
        parent.GraphId.ShouldBe("ut-graph");
        parent.ParentStageId.ShouldBeNull();
    }

    [Fact]
    public void AllFinishedWithoutFailureSucceedsAndAllFinishedWithAFailureIsDegraded()
    {
        var table = new SwarmRunTable();
        table.ApplyLive(Live(
            new SwarmLiveDocument.RunPart { RunId = "a.a1", State = "completed" },
            new SwarmLiveDocument.RunPart { RunId = "b.a1", State = "cancelled" }));

        var succeeded = table.Project(true, false, null)[0];
        succeeded.Status.ShouldBe(StageStatus.Succeeded);
        succeeded.Detail.ShouldBe("Subagents finished: 2, 1 cancelled");

        table.ApplyLive(Live(new SwarmLiveDocument.RunPart { RunId = "c.a1", State = "failed" }));
        var degraded = table.Project(true, false, null)[0];
        degraded.Status.ShouldBe(StageStatus.Degraded);
        degraded.Detail.ShouldBe("Subagents failed: 1 of 3");
    }

    [Fact]
    public void NoRunsProjectsPendingOrDegradedAccordingToWhatWasSeen()
    {
        var table = new SwarmRunTable();

        table.Project(false, false, null).ShouldHaveSingleItem().Detail.ShouldBe("Orchestrator has not started");
        table.Project(true, false, null).ShouldHaveSingleItem().Detail.ShouldBe("No subagents have started yet");
        var degraded = table.Project(true, true, null).ShouldHaveSingleItem();
        degraded.Status.ShouldBe(StageStatus.Degraded);
        degraded.Detail.ShouldBe("Swarm status file could not be read");
    }

    [Fact]
    public void RunCapDropsFinishedRunsFirstAndRefusesNewRunsWhenNoneIsFinished()
    {
        var table = new SwarmRunTable();
        for (var i = 0; i < SwarmRunTable.MaxRuns; i++)
        {
            table.ApplyEvent(State("r" + i + ".a1", "running"));
        }

        table.ApplyEvent(State("extra.a1", "running"));
        table.Count.ShouldBe(SwarmRunTable.MaxRuns);
        table.Project(true, false, null).ShouldNotContain(s => s.StageId == "agent:extra.a1");

        table.ApplyEvent(State("r0.a1", "completed"));
        table.ApplyEvent(State("extra.a1", "running"));

        table.Count.ShouldBe(SwarmRunTable.MaxRuns);
        table.Project(true, false, null).ShouldContain(s => s.StageId == "agent:extra.a1");
        table.Project(true, false, null).ShouldNotContain(s => s.StageId == "agent:r0.a1");
    }

    [Fact]
    public void RunEventsFromTheEventLogDriveTheSameStatesAsTheLiveFile()
    {
        var table = new SwarmRunTable();

        table.ApplyEvent(new SwarmEventRecord { Type = "swarm.run.queued", Payload = new SwarmEventRecord.PayloadPart { RunId = "r.a1", Agent = "a02-requirements" } });
        Child(table, "r.a1").Detail.ShouldBe("Queued");

        table.ApplyEvent(new SwarmEventRecord { Type = "swarm.run.started", Payload = new SwarmEventRecord.PayloadPart { RunId = "r.a1" } });
        Child(table, "r.a1").Detail.ShouldBe("Starting");

        table.ApplyEvent(new SwarmEventRecord { Type = "swarm.run.heartbeat", Payload = new SwarmEventRecord.PayloadPart { RunId = "r.a1", State = "running", Stalled = true } });
        Child(table, "r.a1").Detail.ShouldBe("Running (stalled)");

        table.ApplyEvent(new SwarmEventRecord { Type = "swarm.run.finished", Payload = new SwarmEventRecord.PayloadPart { RunId = "r.a1", State = "failed", ErrorCode = "E-CONTRACT" } });
        var child = Child(table, "r.a1");
        child.Status.ShouldBe(StageStatus.Failed);
        child.Detail.ShouldBe("Failed [E-CONTRACT]");
        child.Title.ShouldBe("a02-requirements");
    }

    [Fact]
    public void UnrelatedEventTypesAndEmptyPayloadsChangeNothing()
    {
        var table = new SwarmRunTable();

        table.ApplyEvent(new SwarmEventRecord { Type = "swarm.tool.call", Payload = new SwarmEventRecord.PayloadPart { RunId = "r.a1", State = "running" } });
        table.ApplyEvent(new SwarmEventRecord { Type = "swarm.run.activity", Payload = new SwarmEventRecord.PayloadPart { RunId = "r.a1" } });
        table.ApplyEvent(new SwarmEventRecord { Type = "swarm.run.state" });
        table.ApplyEvent(new SwarmEventRecord { Payload = new SwarmEventRecord.PayloadPart { RunId = "r.a1" } });

        table.Count.ShouldBe(0);
    }
}
