using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Gsd;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Gsd;

/// <summary>
/// Verifies the workflow phase stage: its states, its degradations, that a poll with nothing new spawns no process, that
/// only changes are emitted, that cancellation ends the sequence quietly, and that a session sees only its own workspace.
/// </summary>
public sealed class GsdStateObserverTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ActivePhaseIsRunningWithPlanCounts()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner { Respond = static _ => GsdFixtures.Load("roadmap-analyze.json") };
        var time = new GsdManualTimeProvider();
        var observer = new GsdStateObserver(GsdTestSupport.NewClient(directory, runner, time), time);

        var first = await FirstEventAsync(observer, GsdTestSupport.Scope(root, "s-7", "ut-graph")).ConfigureAwait(true);

        first.SessionId.ShouldBe("s-7");
        first.Source.ShouldBe(StageSource.Gsd);
        first.StageId.ShouldBe("phase");
        first.Title.ShouldBe("Workflow phase");
        first.Status.ShouldBe(StageStatus.Running);
        first.Detail.ShouldBe("Phase 23: Web Head on the Shared Presentation Layer, 0 of 1 plans");
        first.GraphId.ShouldBe("ut-graph");
        first.Timestamp.ShouldBe(time.GetUtcNow());
    }

    [Fact]
    public async Task CompletePhaseSucceeds()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner { Respond = static _ => GsdFixtures.Load("roadmap-analyze-all-complete.json") };
        var time = new GsdManualTimeProvider();
        var observer = new GsdStateObserver(GsdTestSupport.NewClient(directory, runner, time), time);

        var first = await FirstEventAsync(observer, GsdTestSupport.Scope(root)).ConfigureAwait(true);

        first.Status.ShouldBe(StageStatus.Succeeded);
        first.Detail.ShouldBe("Phase 2: Second, 1 of 1 plans");
    }

    [Fact]
    public async Task WorkspaceWithoutPlanningIsUnavailableAndSpawnsNothing()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspaceWithoutPlanning("ws");
        var runner = new GsdFakeProcessRunner();
        var time = new GsdManualTimeProvider();
        var observer = new GsdStateObserver(GsdTestSupport.NewClient(directory, runner, time), time);

        var first = await FirstEventAsync(observer, GsdTestSupport.Scope(root)).ConfigureAwait(true);

        first.Status.ShouldBe(StageStatus.Unavailable);
        first.Detail.ShouldBe("No workflow state in this workspace");
        runner.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task MissingToolsAreUnavailableWithAReason()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner();
        var time = new GsdManualTimeProvider();
        var observer = new GsdStateObserver(GsdTestSupport.NewClientWithoutTools(directory, runner, time), time);

        var first = await FirstEventAsync(observer, GsdTestSupport.Scope(root)).ConfigureAwait(true);

        first.Status.ShouldBe(StageStatus.Unavailable);
        first.Detail.ShouldNotBeNull().ShouldContain("not installed");
        runner.CallCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("exit")]
    public async Task FailingQueryIsADegradedStageNotAnException(string mode)
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner
        {
            Respond = _ => string.Equals(mode, "exit", StringComparison.Ordinal)
                ? throw new GsdToolsException(GsdToolsFailure.NonZeroExit, "The workflow tool exited with code 1.", 1)
                : mode,
        };
        var time = new GsdManualTimeProvider();
        var observer = new GsdStateObserver(GsdTestSupport.NewClient(directory, runner, time), time);

        var first = await FirstEventAsync(observer, GsdTestSupport.Scope(root)).ConfigureAwait(true);

        first.Status.ShouldBe(StageStatus.Degraded);
        first.Detail.ShouldNotBeNull().ShouldStartWith("Workflow state could not be read: ");
        first.Detail.ShouldNotContain("not json");
    }

    [Fact]
    public async Task TimeoutIsADegradedStage()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner
        {
            Respond = static _ => throw new GsdToolsException(GsdToolsFailure.TimedOut, "The workflow tool did not finish in time and was stopped."),
        };
        var time = new GsdManualTimeProvider();
        var observer = new GsdStateObserver(GsdTestSupport.NewClient(directory, runner, time), time);

        var first = await FirstEventAsync(observer, GsdTestSupport.Scope(root)).ConfigureAwait(true);

        first.Status.ShouldBe(StageStatus.Degraded);
        first.Detail.ShouldNotBeNull().ShouldContain("did not finish in time");
    }

    [Fact]
    public async Task PollsEveryFiveSecondsSpawnsNothingWhileUnchangedAndEmitsOnlyChanges()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner { Respond = static _ => GsdFixtures.Load("roadmap-analyze.json") };
        var time = new GsdManualTimeProvider();
        var observer = new GsdStateObserver(GsdTestSupport.NewClient(directory, runner, time), time);
        var enumerator = observer.ObserveAsync(GsdTestSupport.Scope(root), Token).GetAsyncEnumerator(Token);
        try
        {
            (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
            var firstTimestamp = enumerator.Current.Timestamp;
            var next = enumerator.MoveNextAsync().AsTask();

            // One poll interval passes and nothing changed: no event and, because the planning files are unchanged, no spawn.
            await time.AdvanceAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            await time.WaitForArmedTimerAsync().ConfigureAwait(true);
            runner.CallCount.ShouldBe(1);
            next.IsCompleted.ShouldBeFalse();

            runner.Respond = static _ => GsdFixtures.Load("roadmap-analyze-all-complete.json");
            GsdTempDirectory.RewritePlanningFile(root, "STATE.md", "# State, advanced to the next phase\n");
            await time.AdvanceAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);

            (await next.ConfigureAwait(true)).ShouldBeTrue();
            enumerator.Current.Status.ShouldBe(StageStatus.Succeeded);
            enumerator.Current.Timestamp.ShouldBe(firstTimestamp + TimeSpan.FromSeconds(10));
            runner.CallCount.ShouldBe(2);
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CancellationWhileWaitingEndsTheSequenceWithoutAnException()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner { Respond = static _ => GsdFixtures.Load("roadmap-analyze.json") };
        var time = new GsdManualTimeProvider();
        var observer = new GsdStateObserver(GsdTestSupport.NewClient(directory, runner, time), time);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var enumerator = observer.ObserveAsync(GsdTestSupport.Scope(root), cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        try
        {
            (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
            var next = enumerator.MoveNextAsync().AsTask();
            await time.WaitForArmedTimerAsync().ConfigureAwait(true);

            await cancellation.CancelAsync().ConfigureAwait(true);

            (await next.ConfigureAwait(true)).ShouldBeFalse();
            runner.CallCount.ShouldBe(1);
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task AlreadyCancelledTokenYieldsNothingAndSpawnsNothing()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner { Respond = static _ => GsdFixtures.Load("roadmap-analyze.json") };
        var time = new GsdManualTimeProvider();
        var observer = new GsdStateObserver(GsdTestSupport.NewClient(directory, runner, time), time);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync().ConfigureAwait(true);

        var events = new List<StageEvent>();
        await foreach (var stageEvent in observer.ObserveAsync(GsdTestSupport.Scope(root), cancellation.Token).ConfigureAwait(true))
        {
            events.Add(stageEvent);
        }

        events.ShouldBeEmpty();
        runner.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task EachSessionObservesOnlyItsOwnWorkspace()
    {
        using var directory = new GsdTempDirectory();
        var first = directory.CreateWorkspace("first");
        var second = directory.CreateWorkspace("second");
        var runner = new GsdFakeProcessRunner
        {
            Respond = projectDirectory => string.Equals(projectDirectory, first, StringComparison.Ordinal)
                ? """{"phases":[{"number":"1","name":"Alpha","plan_count":2,"summary_count":1,"disk_status":"executed"}],"current_phase":"1"}"""
                : """{"phases":[{"number":"9","name":"Omega","plan_count":5,"summary_count":0,"disk_status":"planned"}],"current_phase":"9"}""",
        };
        var time = new GsdManualTimeProvider();
        var observer = new GsdStateObserver(GsdTestSupport.NewClient(directory, runner, time), time);

        var one = await FirstEventAsync(observer, GsdTestSupport.Scope(first, "session-a")).ConfigureAwait(true);
        var two = await FirstEventAsync(observer, GsdTestSupport.Scope(second, "session-b")).ConfigureAwait(true);

        one.SessionId.ShouldBe("session-a");
        one.Detail.ShouldBe("Phase 1: Alpha, 1 of 2 plans");
        two.SessionId.ShouldBe("session-b");
        two.Detail.ShouldBe("Phase 9: Omega, 0 of 5 plans");
    }

    private static async Task<StageEvent> FirstEventAsync(GsdStateObserver observer, StageObservationScope scope)
    {
        var enumerator = observer.ObserveAsync(scope, Token).GetAsyncEnumerator(Token);
        try
        {
            (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
            return enumerator.Current;
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(true);
        }
    }
}
