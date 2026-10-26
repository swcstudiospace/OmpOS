using OmpOS.Harness.AgentLoop.Gsd;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Gsd;

/// <summary>
/// Verifies how the client turns the tool's output into phases: the real shape, tolerance for unknown or retyped fields,
/// typed failures, and the per-workspace cache keyed by the planning files.
/// </summary>
public sealed class GsdToolsClientTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RealOutputYieldsPhasesAndTheCurrentPhase()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner { Respond = static _ => GsdFixtures.Load("roadmap-analyze.json") };
        var client = NewClient(directory, runner);

        var snapshot = await client.GetWorkflowAsync(root, Token).ConfigureAwait(true);

        snapshot.Phases.Select(phase => phase.Number).ShouldBe(["20", "21", "22", "23", "25.9"]);
        snapshot.Phases[0].Status.ShouldBe("complete");
        snapshot.Phases[0].PlansComplete.ShouldBe(4);
        snapshot.Phases[0].PlansTotal.ShouldBe(4);
        snapshot.Phases[2].Status.ShouldBe("discussed");
        snapshot.Phases[4].Status.ShouldBe("no directory");
        var current = snapshot.CurrentPhase.ShouldNotBeNull();
        current.Number.ShouldBe("23");
        current.Name.ShouldBe("Web Head on the Shared Presentation Layer");
        current.Status.ShouldBe("planned");
        current.PlansComplete.ShouldBe(0);
        current.PlansTotal.ShouldBe(1);
    }

    [Fact]
    public async Task QueryRunsAgainstTheWorkspaceRootWithTheLocatedScript()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner { Respond = static _ => GsdFixtures.Load("roadmap-analyze.json") };
        var client = NewClient(directory, runner);

        await client.GetWorkflowAsync(root, Token).ConfigureAwait(true);

        runner.Calls.ShouldHaveSingleItem().ShouldBe((directory.PathOf(Path.Combine("tools", "bin", "gsd-tools.cjs")), "roadmap.analyze", root));
    }

    [Fact]
    public async Task UnknownRetypedAndMissingFieldsAreTolerated()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner { Respond = static _ => GsdFixtures.Load("roadmap-analyze-unknown-fields.json") };
        var client = NewClient(directory, runner);

        var snapshot = await client.GetWorkflowAsync(root, Token).ConfigureAwait(true);

        snapshot.Phases.Select(phase => phase.Number).ShouldBe(["7", "8", "9"]);
        snapshot.Phases[1].Name.ShouldBe("(unnamed)");
        snapshot.Phases[1].Status.ShouldBe("executed");
        snapshot.Phases[1].PlansComplete.ShouldBe(1);
        snapshot.Phases[1].PlansTotal.ShouldBe(3);
        snapshot.Phases[2].Status.ShouldBe("unknown");
        snapshot.Phases[2].PlansTotal.ShouldBe(0);
        snapshot.CurrentPhase.ShouldNotBeNull().Number.ShouldBe("8");
    }

    [Fact]
    public async Task EmptyPhaseListIsValidAndHasNoCurrentPhase()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner { Respond = static _ => GsdFixtures.Load("roadmap-analyze-empty.json") };

        var snapshot = await NewClient(directory, runner).GetWorkflowAsync(root, Token).ConfigureAwait(true);

        snapshot.Phases.ShouldBeEmpty();
        snapshot.CurrentPhase.ShouldBeNull();
    }

    [Fact]
    public async Task WhenEveryPhaseIsCompleteTheLastPhaseIsCurrent()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner { Respond = static _ => GsdFixtures.Load("roadmap-analyze-all-complete.json") };

        var snapshot = await NewClient(directory, runner).GetWorkflowAsync(root, Token).ConfigureAwait(true);

        var current = snapshot.CurrentPhase.ShouldNotBeNull();
        current.Number.ShouldBe("2");
        current.Status.ShouldBe("complete");
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"ok\":false,\"reason\":\"sdk_unknown_command\"}")]
    public async Task OutputThatIsNotARoadmapAnalysisIsInvalidOutput(string output)
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner { Respond = _ => output };

        var failure = await Should.ThrowAsync<GsdToolsException>(() => NewClient(directory, runner).GetWorkflowAsync(root, Token)).ConfigureAwait(true);

        failure.Failure.ShouldBe(GsdToolsFailure.InvalidOutput);
        failure.Message.ShouldNotContain("sdk_unknown_command");
    }

    [Theory]
    [InlineData(GsdToolsFailure.NonZeroExit)]
    [InlineData(GsdToolsFailure.TimedOut)]
    [InlineData(GsdToolsFailure.OutputTooLarge)]
    public async Task RunnerFailuresPropagateTypedAndAreNotCached(GsdToolsFailure kind)
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var fail = true;
        var runner = new GsdFakeProcessRunner
        {
            Respond = _ => fail ? throw new GsdToolsException(kind, "tool failed") : GsdFixtures.Load("roadmap-analyze.json"),
        };
        var client = NewClient(directory, runner);

        var failure = await Should.ThrowAsync<GsdToolsException>(() => client.GetWorkflowAsync(root, Token)).ConfigureAwait(true);
        failure.Failure.ShouldBe(kind);

        fail = false;
        var snapshot = await client.GetWorkflowAsync(root, Token).ConfigureAwait(true);

        snapshot.Phases.ShouldNotBeEmpty();
        runner.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task WorkspaceWithoutPlanningDirectoryIsNotInitializedAndSpawnsNothing()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspaceWithoutPlanning("ws");
        var runner = new GsdFakeProcessRunner();

        var failure = await Should.ThrowAsync<GsdToolsException>(() => NewClient(directory, runner).GetWorkflowAsync(root, Token)).ConfigureAwait(true);

        failure.Failure.ShouldBe(GsdToolsFailure.WorkspaceNotInitialized);
        failure.Message.ShouldBe("No workflow state in this workspace");
        runner.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task MissingToolsScriptIsToolsNotFoundAndSpawnsNothing()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner();
        var client = new GsdToolsClient(runner, new GsdToolsLocator(directory.PathOf("absent")), new GsdManualTimeProvider());

        var failure = await Should.ThrowAsync<GsdToolsException>(() => client.GetWorkflowAsync(root, Token)).ConfigureAwait(true);

        failure.Failure.ShouldBe(GsdToolsFailure.ToolsNotFound);
        runner.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SecondPollSpawnsNothingUntilAPlanningFileChanges()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner { Respond = static _ => GsdFixtures.Load("roadmap-analyze.json") };
        var client = NewClient(directory, runner);

        await client.GetWorkflowAsync(root, Token).ConfigureAwait(true);
        await client.GetWorkflowAsync(root, Token).ConfigureAwait(true);
        runner.CallCount.ShouldBe(1);

        GsdTempDirectory.RewritePlanningFile(root, "STATE.md", "# State, now with a longer body\n");
        await client.GetWorkflowAsync(root, Token).ConfigureAwait(true);
        runner.CallCount.ShouldBe(2);

        GsdTempDirectory.RewritePlanningFile(root, "ROADMAP.md", "# Roadmap, now with a longer body\n");
        await client.GetWorkflowAsync(root, Token).ConfigureAwait(true);
        runner.CallCount.ShouldBe(3);
    }

    [Fact]
    public async Task ChangedModificationTimeAloneInvalidatesTheCache()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner { Respond = static _ => GsdFixtures.Load("roadmap-analyze.json") };
        var client = NewClient(directory, runner);
        await client.GetWorkflowAsync(root, Token).ConfigureAwait(true);

        File.SetLastWriteTimeUtc(Path.Combine(root, ".planning", "ROADMAP.md"), DateTime.UtcNow.AddHours(2));
        await client.GetWorkflowAsync(root, Token).ConfigureAwait(true);

        runner.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task CachedResultExpiresAfterTheMaximumAge()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner { Respond = static _ => GsdFixtures.Load("roadmap-analyze.json") };
        var time = new GsdManualTimeProvider();
        var client = new GsdToolsClient(runner, new GsdToolsLocator(directory.CreateToolsHome("tools")), time, TimeSpan.FromSeconds(30));
        await client.GetWorkflowAsync(root, Token).ConfigureAwait(true);

        time.MoveForward(TimeSpan.FromSeconds(29));
        await client.GetWorkflowAsync(root, Token).ConfigureAwait(true);
        runner.CallCount.ShouldBe(1);

        time.MoveForward(TimeSpan.FromSeconds(2));
        await client.GetWorkflowAsync(root, Token).ConfigureAwait(true);
        runner.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task EachWorkspaceHasItsOwnCacheEntryAndResult()
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
        var client = NewClient(directory, runner);

        var one = await client.GetWorkflowAsync(first, Token).ConfigureAwait(true);
        var two = await client.GetWorkflowAsync(second, Token).ConfigureAwait(true);
        var oneAgain = await client.GetWorkflowAsync(first, Token).ConfigureAwait(true);

        one.CurrentPhase.ShouldNotBeNull().Name.ShouldBe("Alpha");
        two.CurrentPhase.ShouldNotBeNull().Name.ShouldBe("Omega");
        oneAgain.CurrentPhase.ShouldNotBeNull().Name.ShouldBe("Alpha");
        runner.Calls.Select(call => call.ProjectDirectory).ShouldBe([first, second]);
    }

    [Fact]
    public async Task CancelledTokenThrowsBeforeAnythingRuns()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(() => NewClient(directory, runner).GetWorkflowAsync(root, cancellation.Token)).ConfigureAwait(true);

        runner.CallCount.ShouldBe(0);
    }

    private static GsdToolsClient NewClient(GsdTempDirectory directory, GsdFakeProcessRunner runner) =>
        new(runner, new GsdToolsLocator(directory.CreateToolsHome("tools")), new GsdManualTimeProvider());
}
