using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Stages;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Stages;

/// <summary>
/// Verifies the prompt-plan stage: pending until the planner's record exists, the three outcomes of a readable record, the
/// degraded outcome of an unreadable one, that polling continues and re-emits only on change, that cancellation ends the
/// sequence, and that a session never reads another session's directory.
/// </summary>
public sealed class UltrathinkStateObserverTests
{
    private static UltrathinkStateObserver NewObserver(StageFakeTimeProvider clock, bool planningEnabled = true) =>
        new(new UltrathinkStateObserverOptions { PlanningEnabled = planningEnabled, PollInterval = StageFixtures.Interval }, clock);

    private static Task<StageObserverRun> StartAsync(UltrathinkStateObserver observer, StageObservationScope scope, StageFakeTimeProvider clock) =>
        StageObserverRun.StartAsync(observer, scope, clock, StageFixtures.Interval);

    [Fact]
    public async Task PlanningSwitchedOffIsSkippedWithoutReadingAnyFileAndTheSequenceEnds()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WritePlan(scope, StageFixtures.Bytes("ultrathink-session-complete.json"));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewObserver(clock, planningEnabled: false), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        await run.Completion.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken).ConfigureAwait(true);

        var only = run.Events.ShouldHaveSingleItem();
        only.StageId.ShouldBe("plan");
        only.Title.ShouldBe("Prompt plan");
        only.Source.ShouldBe(StageSource.Ultrathink);
        only.Status.ShouldBe(StageStatus.Skipped);
        only.Detail.ShouldBe("Planning is switched off for this run");
        clock.TimersCreated.ShouldBe(0);
    }

    [Fact]
    public async Task PendingWhileTheEngineSessionIsUnknownAndNothingIsReadMeanwhile()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope(acpSessionId: null);
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewObserver(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        await run.StepAsync(3).ConfigureAwait(true);

        var only = run.Events.ShouldHaveSingleItem();
        only.Status.ShouldBe(StageStatus.Pending);
        only.StageId.ShouldBe("plan");
        only.SessionId.ShouldBe("s-1");
        Directory.Exists(directory.Scope().SessionStateDirectory).ShouldBeFalse();
    }

    [Fact]
    public async Task PendingUntilTheRecordAppearsThenSucceededWithCountsAndGraphId()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewObserver(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        await run.StepAsync(2).ConfigureAwait(true);
        run.Events.ShouldHaveSingleItem().Status.ShouldBe(StageStatus.Pending);

        StageTempDirectory.WritePlan(scope, StageFixtures.Bytes("ultrathink-session-complete.json"));
        await run.StepAsync().ConfigureAwait(true);

        var plan = run.Events.Last(e => string.Equals(e.StageId, "plan", StringComparison.Ordinal));
        plan.Status.ShouldBe(StageStatus.Succeeded);
        plan.Detail.ShouldBe("8 nodes, 6 waves, 6 clarifications");
        plan.GraphId.ShouldBe("ut-muh4aveq-080f3f30");
        plan.Timestamp.ShouldBe(clock.GetUtcNow());
        run.Events.Count(e => string.Equals(e.StageId, "tracking", StringComparison.Ordinal)).ShouldBe(0);
    }

    [Fact]
    public async Task PartialTrackingAddsADegradedTrackingStageThatNamesTheStatusAndNoUrl()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var text = StageFixtures.Text("ultrathink-session-partial.json")
            .Replace("tracker rows were not created", "https://tracker.example.test/rows?token=SECRET-VALUE", StringComparison.Ordinal);
        StageTempDirectory.WritePlan(scope, StageFixtures.Utf8(text));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewObserver(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        var plan = run.Events.Single(e => string.Equals(e.StageId, "plan", StringComparison.Ordinal));
        plan.Status.ShouldBe(StageStatus.Succeeded);
        plan.Detail.ShouldBe("8 nodes, 4 waves, 3 clarifications");
        var tracking = run.Events.Single(e => string.Equals(e.StageId, "tracking", StringComparison.Ordinal));
        tracking.Status.ShouldBe(StageStatus.Degraded);
        tracking.Detail.ShouldBe("Issue tracking status: partial");
        tracking.ParentStageId.ShouldBeNull();
        run.Events.ShouldAllBe(e => !(e.Detail ?? string.Empty).Contains("SECRET", StringComparison.Ordinal)
            && !(e.Detail ?? string.Empty).Contains("http", StringComparison.Ordinal)
            && !e.Title.Contains("http", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FallbackRecordIsDegradedWithTheFallbackDetail()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WritePlan(scope, StageFixtures.Bytes("ultrathink-session-fallback.json"));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewObserver(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        var plan = run.Events.ShouldHaveSingleItem();
        plan.Status.ShouldBe(StageStatus.Degraded);
        plan.Detail.ShouldBe("Planner fell back to a basic plan: no graph");
        plan.GraphId.ShouldBeNull();
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("[1,2,3]")]
    public async Task UnreadableRecordIsDegradedPollingContinuesAndTheNextGoodRecordSucceeds(string content)
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WritePlan(scope, StageFixtures.Utf8(content));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewObserver(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        await run.StepAsync(2).ConfigureAwait(true);

        var degraded = run.Events.ShouldHaveSingleItem();
        degraded.Status.ShouldBe(StageStatus.Degraded);
        degraded.Detail.ShouldBe("Plan file could not be read");

        StageTempDirectory.WritePlan(scope, StageFixtures.Bytes("ultrathink-session-complete.json"));
        await run.StepAsync().ConfigureAwait(true);

        run.Events.Count.ShouldBe(2);
        run.Events[1].Status.ShouldBe(StageStatus.Succeeded);
        run.Completion.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task OversizeRecordIsDegradedWithoutBeingParsedAndPollingContinues()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WritePlan(scope, new byte[(2 * 1024 * 1024) + 1]);
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewObserver(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        run.Events.ShouldHaveSingleItem().Detail.ShouldBe("Plan file could not be read");

        StageTempDirectory.WritePlan(scope, StageFixtures.Bytes("ultrathink-session-partial.json"));
        await run.StepAsync().ConfigureAwait(true);

        run.Events.Last(e => string.Equals(e.StageId, "plan", StringComparison.Ordinal)).Status.ShouldBe(StageStatus.Succeeded);
    }

    [Fact]
    public async Task RecordExactlyAtTheCapIsStillRead()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var valid = StageFixtures.Text("ultrathink-session-complete.json").TrimEnd();
        var padded = valid[..^1] + ",\"pad\":\"" + new string('x', (2 * 1024 * 1024) - valid.Length - 9) + "\"}";
        StageFixtures.Utf8(padded).Length.ShouldBe(2 * 1024 * 1024);
        StageTempDirectory.WritePlan(scope, StageFixtures.Utf8(padded));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewObserver(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        run.Events.Single(e => string.Equals(e.StageId, "plan", StringComparison.Ordinal)).Status.ShouldBe(StageStatus.Succeeded);
    }

    [Fact]
    public async Task UnchangedRecordEmitsNothingAndAChangedRecordEmitsOnlyTheChange()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var partial = StageFixtures.Text("ultrathink-session-partial.json");
        StageTempDirectory.WritePlan(scope, StageFixtures.Utf8(partial));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewObserver(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        await run.StepAsync(4).ConfigureAwait(true);
        run.Events.Count.ShouldBe(2);

        StageTempDirectory.WritePlan(scope, StageFixtures.Utf8(partial.Replace("\"partial\"", "\"complete\"", StringComparison.Ordinal)));
        await run.StepAsync().ConfigureAwait(true);

        run.Events.Count.ShouldBe(3);
        var recovered = run.Events[2];
        recovered.StageId.ShouldBe("tracking");
        recovered.Status.ShouldBe(StageStatus.Succeeded);
        recovered.Detail.ShouldBe("Issue tracking status: complete");

        await run.StepAsync(2).ConfigureAwait(true);
        run.Events.Count.ShouldBe(3);
    }

    [Fact]
    public async Task RecordRemovedAfterItWasReadKeepsTheLastKnownState()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        StageTempDirectory.WritePlan(scope, StageFixtures.Bytes("ultrathink-session-complete.json"));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewObserver(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        File.Delete(PlanPath(scope));
        await run.StepAsync(2).ConfigureAwait(true);

        run.Events.ShouldHaveSingleItem().Status.ShouldBe(StageStatus.Succeeded);
    }

    [Fact]
    public async Task CancellationEndsTheSequenceWithoutAdvancingTheClock()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewObserver(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        run.Completion.IsCompleted.ShouldBeFalse();

        await run.StopAsync().ConfigureAwait(true);

        run.Completion.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task ASessionNeverReadsAnotherSessionsDirectory()
    {
        using var directory = new StageTempDirectory();
        var mine = directory.Scope("session-a");
        var other = directory.Scope("session-b");
        StageTempDirectory.WritePlan(other, StageFixtures.Bytes("ultrathink-session-complete.json"));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewObserver(clock), mine, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        await run.StepAsync(2).ConfigureAwait(true);

        var only = run.Events.ShouldHaveSingleItem();
        only.Status.ShouldBe(StageStatus.Pending);
        only.SessionId.ShouldBe("session-a");

        StageTempDirectory.WritePlan(mine, StageFixtures.Bytes("ultrathink-session-fallback.json"));
        await run.StepAsync().ConfigureAwait(true);

        var latest = run.Events[^1];
        latest.Status.ShouldBe(StageStatus.Degraded);
        latest.SessionId.ShouldBe("session-a");
    }

    [Fact]
    public async Task EngineSessionIdThatTriesToEscapeTheSessionDirectoryReadsNothingOutsideIt()
    {
        using var directory = new StageTempDirectory();
        var outside = directory.Scope("outside");
        StageTempDirectory.WritePlan(outside, StageFixtures.Bytes("ultrathink-session-complete.json"));
        var scope = directory.Scope("inside", acpSessionId: "../../../outside/ultrathink/sessions/" + outside.AcpSessionId);
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewObserver(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        run.Events.ShouldHaveSingleItem().Status.ShouldBe(StageStatus.Pending);
    }

    [Fact]
    public void ObserverRejectsANonPositivePollInterval()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new UltrathinkStateObserver(new UltrathinkStateObserverOptions { PollInterval = TimeSpan.Zero }));
    }

    private static string PlanPath(StageObservationScope scope) =>
        OmpOS.Harness.AgentLoop.Launch.SessionStateLayout.UltrathinkSessionFile(scope.SessionStateDirectory, scope.AcpSessionId!);
}
