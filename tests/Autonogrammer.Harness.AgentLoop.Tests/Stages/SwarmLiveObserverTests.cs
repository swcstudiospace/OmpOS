using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Stages;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Stages;

/// <summary>
/// Verifies the subagent stages: the state mapping against fixtures, the incremental event log, every degraded input (empty,
/// malformed, wrong schema, oversize, partial last line, shrinking file), that a finished run leaves the Running state, that
/// no free text reaches an event, polling and cancellation, and that a session never reads another session's directory.
/// </summary>
public sealed class SwarmLiveObserverTests
{
    private static SwarmLiveObserver NewObserver(StageFakeTimeProvider clock) =>
        new(new SwarmLiveObserverOptions { PollInterval = StageFixtures.Interval }, clock);

    private static Task<StageObserverRun> StartAsync(StageObservationScope scope, StageFakeTimeProvider clock) =>
        StageObserverRun.StartAsync(NewObserver(clock), scope, clock, StageFixtures.Interval);

    private static StageEvent Latest(StageObserverRun run, string stageId) => run.Events.Last(e => string.Equals(e.StageId, stageId, StringComparison.Ordinal));

    private static StageStatus[] History(StageObserverRun run, string stageId) => [.. run.Events.Where(e => string.Equals(e.StageId, stageId, StringComparison.Ordinal)).Select(static e => e.Status)];

    private static byte[] LiveJson(params (string Id, string Agent, string State, string? Error)[] runs)
    {
        var builder = new StringBuilder("{\"schema\":\"swarm.live.v1\",\"correlation_id\":null,\"seq\":1,\"updated_wall\":1.0,\"runs\":[");
        for (var i = 0; i < runs.Length; i++)
        {
            var (id, agent, state, error) = runs[i];
            builder.Append(i == 0 ? string.Empty : ",")
                .Append("{\"run_id\":\"").Append(JsonEncodedText.Encode(id).ToString())
                .Append("\",\"agent\":\"").Append(JsonEncodedText.Encode(agent).ToString())
                .Append("\",\"state\":\"").Append(JsonEncodedText.Encode(state).ToString())
                .Append("\",\"error\":").Append(error is null ? "null" : "\"" + JsonEncodedText.Encode(error).ToString() + "\"")
                .Append('}');
        }

        return StageFixtures.Utf8(builder.Append("]}").ToString());
    }

    [Fact]
    public async Task NoFilesYetIsPendingAndStaysQuiet()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope(graphId: "ut-g1");
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        await run.StepAsync(3).ConfigureAwait(true);

        var only = run.Events.ShouldHaveSingleItem();
        only.StageId.ShouldBe("swarm");
        only.Title.ShouldBe("Subagents");
        only.Source.ShouldBe(StageSource.Swarm);
        only.Status.ShouldBe(StageStatus.Pending);
        only.Detail.ShouldBe("Orchestrator has not started");
        only.SessionId.ShouldBe("s-1");
        only.GraphId.ShouldBe("ut-g1");
        only.ParentStageId.ShouldBeNull();
    }

    [Fact]
    public async Task LiveFixtureMapsEveryRunToAChildStageAndTheSwarmToRunningWithCounters()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope(graphId: "ut-g1");
        StageTempDirectory.WriteLive(scope, StageFixtures.Bytes("swarm-live-running.json"));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        run.Events.Count.ShouldBe(5);
        var parent = run.Events[0];
        parent.StageId.ShouldBe("swarm");
        parent.Status.ShouldBe(StageStatus.Running);
        parent.Detail.ShouldBe("2 running, 1 queued, 1 completed, 0 failed");
        parent.GraphId.ShouldBe("ut-g1");

        var children = run.Events.Skip(1).ToDictionary(e => e.StageId, StringComparer.Ordinal);
        children.Keys.ShouldBe(["agent:T-req.a1", "agent:T-be.a1", "agent:T-fe.a1", "agent:T-qa.a1"], ignoreOrder: true);
        children["agent:T-req.a1"].Status.ShouldBe(StageStatus.Succeeded);
        children["agent:T-be.a1"].Status.ShouldBe(StageStatus.Running);
        children["agent:T-fe.a1"].Status.ShouldBe(StageStatus.Running);
        children["agent:T-qa.a1"].Status.ShouldBe(StageStatus.Pending);
        children["agent:T-be.a1"].Title.ShouldBe("a05-backend");
        children.Values.ShouldAllBe(e => e.ParentStageId == "swarm" && e.SessionId == "s-1" && e.Source == StageSource.Swarm && e.Timestamp == clock.GetUtcNow());
    }

    [Fact]
    public async Task NoFreeTextFromTheFilesReachesAnEvent()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WriteLive(scope, StageFixtures.Bytes("swarm-live-running.json"));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        run.Events.ShouldAllBe(e => !(e.Detail ?? string.Empty).Contains("OrderService", StringComparison.Ordinal) && !e.Title.Contains("OrderService", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AChildLeavesRunningWhenItsRunCompletes()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WriteLive(scope, LiveJson(("T-a.a1", "a05-backend", "running", null), ("T-b.a1", "a06-frontend", "running", null)));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        Latest(run, "agent:T-a.a1").Status.ShouldBe(StageStatus.Running);

        StageTempDirectory.WriteLive(scope, LiveJson(("T-a.a1", "a05-backend", "completed", null), ("T-b.a1", "a06-frontend", "running", null)));
        await run.StepAsync().ConfigureAwait(true);

        History(run, "agent:T-a.a1").ShouldBe([StageStatus.Running, StageStatus.Succeeded]);
        Latest(run, "agent:T-b.a1").Status.ShouldBe(StageStatus.Running);
        Latest(run, "swarm").Status.ShouldBe(StageStatus.Running);
        Latest(run, "swarm").Detail.ShouldBe("1 running, 0 queued, 1 completed, 0 failed");

        StageTempDirectory.WriteLive(scope, LiveJson(("T-a.a1", "a05-backend", "completed", null), ("T-b.a1", "a06-frontend", "completed", null)));
        await run.StepAsync().ConfigureAwait(true);

        History(run, "agent:T-b.a1").ShouldBe([StageStatus.Running, StageStatus.Succeeded]);
        var parent = Latest(run, "swarm");
        parent.Status.ShouldBe(StageStatus.Succeeded);
        parent.Detail.ShouldBe("Subagents finished: 2");
        run.Events.Where(e => e.Status == StageStatus.Running && !string.Equals(e.StageId, "swarm", StringComparison.Ordinal)).Select(e => e.StageId).ShouldBe(["agent:T-a.a1", "agent:T-b.a1"], ignoreOrder: true);
    }

    [Fact]
    public async Task FailedRunIsFailedOnTheChildAndTheParentIsDegradedOnceEveryoneFinished()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WriteLive(scope, LiveJson(("T-a.a1", "a05-backend", "failed", "E-TIMEOUT"), ("T-b.a1", "a06-frontend", "running", null)));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        var failedChild = Latest(run, "agent:T-a.a1");
        failedChild.Status.ShouldBe(StageStatus.Failed);
        failedChild.Detail.ShouldBe("Failed [E-TIMEOUT]");
        Latest(run, "swarm").Status.ShouldBe(StageStatus.Running);
        Latest(run, "swarm").Detail.ShouldBe("1 running, 0 queued, 0 completed, 1 failed");

        StageTempDirectory.WriteLive(scope, LiveJson(("T-a.a1", "a05-backend", "failed", "E-TIMEOUT"), ("T-b.a1", "a06-frontend", "completed", null)));
        await run.StepAsync().ConfigureAwait(true);

        var parent = Latest(run, "swarm");
        parent.Status.ShouldBe(StageStatus.Degraded);
        parent.Detail.ShouldBe("Subagents failed: 1 of 2");
        Latest(run, "agent:T-a.a1").Status.ShouldBe(StageStatus.Failed);
    }

    [Fact]
    public async Task UnknownStateIsRunningWithTheRawStateAsDetail()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WriteLive(scope, LiveJson(("T-a.a1", "a05-backend", "hibernating", null)));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        var child = Latest(run, "agent:T-a.a1");
        child.Status.ShouldBe(StageStatus.Running);
        child.Detail.ShouldBe("hibernating");
        Latest(run, "swarm").Status.ShouldBe(StageStatus.Running);
    }

    [Fact]
    public async Task EventLogAloneBuildsTheRunsAndIgnoresEveryOtherEventType()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WriteEvents(scope, StageFixtures.Bytes("swarm-events.jsonl"));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        var child = Latest(run, "agent:T-be.a1");
        child.Status.ShouldBe(StageStatus.Succeeded);
        child.Title.ShouldBe("a05-backend");
        Latest(run, "swarm").Status.ShouldBe(StageStatus.Succeeded);
        Latest(run, "swarm").Detail.ShouldBe("Subagents finished: 1");
        run.Events.Count.ShouldBe(2);
    }

    [Fact]
    public async Task EventLogIsTailedIncrementallyAndAPartialLastLineWaitsForItsTerminator()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WriteEvents(scope, StageFixtures.EventLine("swarm.run.queued", "{\"run_id\":\"T-x.a1\",\"agent\":\"a05-backend\"}"));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        Latest(run, "agent:T-x.a1").Detail.ShouldBe("Queued");

        StageTempDirectory.AppendEvents(scope, StageFixtures.EventLine("swarm.run.state", "{\"run_id\":\"T-x.a1\",\"to\":\"running\"}"));
        await run.StepAsync().ConfigureAwait(true);
        Latest(run, "agent:T-x.a1").Status.ShouldBe(StageStatus.Running);

        var finished = StageFixtures.EventLine("swarm.run.finished", "{\"run_id\":\"T-x.a1\",\"state\":\"completed\"}");
        StageTempDirectory.AppendEvents(scope, finished[..(finished.Length / 2)]);
        await run.StepAsync(2).ConfigureAwait(true);
        Latest(run, "agent:T-x.a1").Status.ShouldBe(StageStatus.Running);

        StageTempDirectory.AppendEvents(scope, finished[(finished.Length / 2)..]);
        await run.StepAsync().ConfigureAwait(true);

        History(run, "agent:T-x.a1").ShouldBe([StageStatus.Pending, StageStatus.Running, StageStatus.Succeeded]);
    }

    [Fact]
    public async Task MalformedAndOversizeEventLinesAreSkippedAndTheLinesAroundThemStillCount()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var padding = new string('p', SwarmEventTail.MaxLineBytes + 100);
        var content = new List<byte>();
        content.AddRange(StageFixtures.Utf8("{\"type\": \"swarm.run.state\", \"payload\": {\"run_id\": \n"));
        content.AddRange(StageFixtures.Utf8("this \"swarm.run.state\" line is not json\n"));
        content.AddRange(StageFixtures.EventLine("swarm.run.state", "{\"run_id\":\"T-big.a1\",\"to\":\"running\",\"pad\":\"" + padding + "\"}"));
        content.AddRange(StageFixtures.EventLine("swarm.run.state", "{\"run_id\":\"T-ok.a1\",\"to\":\"running\"}"));
        content.AddRange(StageFixtures.EventLine("swarm.run.state", "{\"run_id\":7,\"to\":\"running\"}"));
        StageTempDirectory.WriteEvents(scope, [.. content]);
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        run.Events.Select(e => e.StageId).ShouldBe(["swarm", "agent:T-ok.a1"]);
    }

    [Fact]
    public async Task ShrunkEventLogIsReadAgainFromTheStart()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var first = new List<byte>();
        first.AddRange(StageFixtures.EventLine("swarm.run.state", "{\"run_id\":\"T-old.a1\",\"to\":\"running\",\"agent\":\"a05-backend\"}"));
        first.AddRange(StageFixtures.EventLine("swarm.run.state", "{\"run_id\":\"T-old2.a1\",\"to\":\"running\",\"agent\":\"a05-backend\"}"));
        StageTempDirectory.WriteEvents(scope, [.. first]);
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        StageTempDirectory.WriteEvents(scope, StageFixtures.EventLine("swarm.run.queued", "{\"run_id\":\"T-new.a1\"}"));
        await run.StepAsync().ConfigureAwait(true);

        Latest(run, "agent:T-new.a1").Detail.ShouldBe("Queued");
    }

    [Fact]
    public async Task AttachingToAHugeLogReadsItsTailInsteadOfItsHistory()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var spam = StageFixtures.EventLine("swarm.tool.call", "{\"tool\":\"orch_registry_ping\",\"caller\":\"A01\",\"state\":\"SUCCESS\",\"pad\":\"" + new string('s', 150) + "\"}");
        var content = new List<byte>();
        content.AddRange(StageFixtures.EventLine("swarm.run.state", "{\"run_id\":\"T-ancient.a1\",\"to\":\"running\"}"));
        for (var i = 0; i < 4000; i++)
        {
            content.AddRange(spam);
        }

        content.AddRange(StageFixtures.EventLine("swarm.run.state", "{\"run_id\":\"T-recent.a1\",\"to\":\"running\"}"));
        StageTempDirectory.WriteEvents(scope, [.. content]);
        new FileInfo(StageTempDirectory.EventsPath(scope)).Length.ShouldBeGreaterThan(SwarmEventTail.MaxChunkBytes * 2L);
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        run.Events.Select(e => e.StageId).ShouldContain("agent:T-recent.a1", StringComparer.Ordinal);
        run.Events.Select(e => e.StageId).ShouldNotContain("agent:T-ancient.a1", StringComparer.Ordinal);
    }

    [Fact]
    public async Task EmptyEventLogAloneIsPendingWithoutSubagents()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WriteEvents(scope, []);
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        var only = run.Events.ShouldHaveSingleItem();
        only.Status.ShouldBe(StageStatus.Pending);
        only.Detail.ShouldBe("No subagents have started yet");
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{\"schema\":\"swarm.live.v2\",\"runs\":[]}")]
    [InlineData("{\"runs\":[]}")]
    public async Task UnusableLiveFileIsDegradedAndRecoversWhenAGoodOneAppears(string content)
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WriteLive(scope, StageFixtures.Utf8(content));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        await run.StepAsync(2).ConfigureAwait(true);

        var degraded = run.Events.ShouldHaveSingleItem();
        degraded.StageId.ShouldBe("swarm");
        degraded.Status.ShouldBe(StageStatus.Degraded);
        degraded.Detail.ShouldBe("Swarm status file could not be read");

        StageTempDirectory.WriteLive(scope, LiveJson(("T-a.a1", "a05-backend", "running", null)));
        await run.StepAsync().ConfigureAwait(true);

        Latest(run, "swarm").Status.ShouldBe(StageStatus.Running);
        Latest(run, "agent:T-a.a1").Status.ShouldBe(StageStatus.Running);
    }

    [Fact]
    public async Task OversizeLiveFileIsDegradedWithoutBeingParsed()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WriteLive(scope, new byte[SwarmLiveObserver.MaxLiveFileBytes + 1]);
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        run.Events.ShouldHaveSingleItem().Status.ShouldBe(StageStatus.Degraded);
    }

    [Fact]
    public async Task BrokenLiveFileDoesNotHideRunsTheEventLogStillShows()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WriteLive(scope, StageFixtures.Utf8("{ broken"));
        StageTempDirectory.WriteEvents(scope, StageFixtures.EventLine("swarm.run.state", "{\"run_id\":\"T-a.a1\",\"to\":\"running\"}"));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        Latest(run, "swarm").Status.ShouldBe(StageStatus.Running);
        Latest(run, "agent:T-a.a1").Status.ShouldBe(StageStatus.Running);
    }

    [Fact]
    public async Task AgentNamesFromTheFileAreCleanedAndBounded()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WriteLive(scope, LiveJson(("T-a.a1", "evil\u001b[2J\nname" + new string('n', 300), "running", null), ("../../etc/passwd", "x", "running", null)));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        var child = run.Events.Single(e => string.Equals(e.StageId, "agent:T-a.a1", StringComparison.Ordinal));
        child.Title.Length.ShouldBeLessThanOrEqualTo(64);
        child.Title.ShouldAllBe(static ch => !char.IsControl(ch));
        run.Events.Count.ShouldBe(2);
        run.Events.ShouldAllBe(e => (e.Detail ?? string.Empty).Length <= StageText.MaxDetailLength);
    }

    [Fact]
    public async Task PollingWithNothingNewEmitsNothing()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WriteLive(scope, StageFixtures.Bytes("swarm-live-running.json"));
        StageTempDirectory.WriteEvents(scope, StageFixtures.Bytes("swarm-events.jsonl"));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        var afterFirstPoll = run.Events.Count;
        await run.StepAsync(5).ConfigureAwait(true);

        run.Events.Count.ShouldBe(afterFirstPoll);
    }

    [Fact]
    public async Task CancellationEndsTheSequenceWithoutAdvancingTheClock()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        run.Completion.IsCompleted.ShouldBeFalse();

        await run.StopAsync().ConfigureAwait(true);

        run.Completion.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task ASessionNeverReadsAnotherSessionsSwarmDirectory()
    {
        using var directory = new StageTempDirectory();
        var mine = directory.Scope("session-a");
        var other = directory.Scope("session-b");
        StageTempDirectory.WriteLive(other, LiveJson(("secret.a1", "a30-custody", "running", null)));
        StageTempDirectory.WriteEvents(other, StageFixtures.EventLine("swarm.run.state", "{\"run_id\":\"secret.a2\",\"to\":\"running\"}"));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(mine, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        await run.StepAsync(2).ConfigureAwait(true);

        var only = run.Events.ShouldHaveSingleItem();
        only.Status.ShouldBe(StageStatus.Pending);
        only.SessionId.ShouldBe("session-a");

        StageTempDirectory.WriteLive(mine, LiveJson(("mine.a1", "a05-backend", "running", null)));
        await run.StepAsync().ConfigureAwait(true);

        run.Events.Select(e => e.StageId).ShouldBe(["swarm", "swarm", "agent:mine.a1"]);
        run.Events.ShouldAllBe(e => e.SessionId == "session-a");
    }

    [Fact]
    public void ObserverRejectsANonPositivePollInterval()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SwarmLiveObserver(new SwarmLiveObserverOptions { PollInterval = TimeSpan.FromSeconds(-1) }));
    }
}
