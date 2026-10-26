using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Gsd;
using OmpOS.Harness.AgentLoop.Substrate;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Gsd;

/// <summary>
/// Verifies the merge of the workflow phase and the optional substrate snapshot into a task context: every part is optional,
/// every unreadable part becomes a plain-text warning, nothing but cancellation escapes, and one scope never sees another's data.
/// </summary>
public sealed class GsdTaskContextSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PhaseAndSubstrateAreMergedAndTheSubstrateTitleWins()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var memory = new ContextNote { Id = "m1", Text = "Use podman, never docker", Origin = "repo" };
        var lesson = new ContextNote { Id = "l1", Text = "Run the gate before pushing", Origin = "lessons" };
        var reader = new GsdFakeSubstrateReader
        {
            Respond = static (_, _) => new SubstrateContextSnapshot
            {
                SharedMemory = [new ContextNote { Id = "m1", Text = "Use podman, never docker", Origin = "repo" }],
                Lessons = [new ContextNote { Id = "l1", Text = "Run the gate before pushing", Origin = "lessons" }],
                TaskId = "SPE-1234",
                Title = "Add the agent panel",
                KanbanStatus = "In Progress",
                Warnings = ["Substrate: lessons are older than a day."],
            },
        };
        var source = new GsdTaskContextSource(NewClient(directory, GsdFixtures.Load("roadmap-analyze.json")), reader);

        var context = await source.GetAsync(GsdTestSupport.Scope(root, graphId: "ut-graph"), Token).ConfigureAwait(true);

        context.Title.ShouldBe("Add the agent panel");
        context.TaskId.ShouldBe("SPE-1234");
        context.KanbanStatus.ShouldBe("In Progress");
        context.GraphId.ShouldBe("ut-graph");
        var phase = context.Phase.ShouldNotBeNull();
        phase.Number.ShouldBe("23");
        phase.PlansTotal.ShouldBe(1);
        context.SharedMemory.ShouldBe([memory]);
        context.Lessons.ShouldBe([lesson]);
        context.Warnings.ShouldBe(["Substrate: lessons are older than a day."]);
    }

    [Fact]
    public async Task WithoutASubstrateReaderTheTitleComesFromThePhaseAndNothingIsWarned()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var source = new GsdTaskContextSource(NewClient(directory, GsdFixtures.Load("roadmap-analyze.json")), substrateReader: null);

        var context = await source.GetAsync(GsdTestSupport.Scope(root), Token).ConfigureAwait(true);

        context.Title.ShouldBe("Phase 23: Web Head on the Shared Presentation Layer");
        context.Phase.ShouldNotBeNull().Name.ShouldBe("Web Head on the Shared Presentation Layer");
        context.TaskId.ShouldBeNull();
        context.KanbanStatus.ShouldBeNull();
        context.SharedMemory.ShouldBeEmpty();
        context.Lessons.ShouldBeEmpty();
        context.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubstrateSnapshotWithoutATitleFallsBackToThePhaseTitle()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var reader = new GsdFakeSubstrateReader
        {
            Respond = static (_, _) => new SubstrateContextSnapshot { SharedMemory = [], Lessons = [], Title = "   ", TaskId = "T-1", Warnings = [] },
        };
        var source = new GsdTaskContextSource(NewClient(directory, GsdFixtures.Load("roadmap-analyze.json")), reader);

        var context = await source.GetAsync(GsdTestSupport.Scope(root), Token).ConfigureAwait(true);

        context.Title.ShouldBe("Phase 23: Web Head on the Shared Presentation Layer");
        context.TaskId.ShouldBe("T-1");
    }

    [Fact]
    public async Task WorkspaceWithoutPlanningLeavesThePhaseOutWithAReason()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspaceWithoutPlanning("ws");
        var reader = new GsdFakeSubstrateReader
        {
            Respond = static (_, _) => new SubstrateContextSnapshot { SharedMemory = [], Lessons = [], Title = "Task from substrate", Warnings = [] },
        };
        var source = new GsdTaskContextSource(NewClient(directory, "{}"), reader);

        var context = await source.GetAsync(GsdTestSupport.Scope(root), Token).ConfigureAwait(true);

        context.Phase.ShouldBeNull();
        context.Title.ShouldBe("Task from substrate");
        context.Warnings.ShouldHaveSingleItem().ShouldBe("Workflow phase is unknown: No workflow state in this workspace.");
    }

    [Fact]
    public async Task MissingToolsLeaveThePhaseOutWithAReason()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner();
        var client = GsdTestSupport.NewClientWithoutTools(directory, runner);
        var source = new GsdTaskContextSource(client);

        var context = await source.GetAsync(GsdTestSupport.Scope(root), Token).ConfigureAwait(true);

        context.Phase.ShouldBeNull();
        context.Title.ShouldBe("Untitled task");
        context.Warnings.ShouldHaveSingleItem().ShouldStartWith("Workflow phase is unknown: ");
        runner.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task FailingSubstrateReaderIsAWarningThatDoesNotQuoteTheFailure()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var reader = new GsdFakeSubstrateReader
        {
            Respond = static (_, _) => throw new InvalidOperationException("bearer SECRET-TOKEN-789 rejected"),
        };
        var source = new GsdTaskContextSource(NewClient(directory, GsdFixtures.Load("roadmap-analyze.json")), reader);

        var context = await source.GetAsync(GsdTestSupport.Scope(root), Token).ConfigureAwait(true);

        context.Phase.ShouldNotBeNull().Number.ShouldBe("23");
        context.Title.ShouldBe("Phase 23: Web Head on the Shared Presentation Layer");
        var warning = context.Warnings.ShouldHaveSingleItem();
        warning.ShouldContain("Substrate context is unavailable");
        warning.ShouldContain(nameof(InvalidOperationException));
        warning.ShouldNotContain("SECRET-TOKEN-789");
    }

    [Fact]
    public async Task EveryPartFailingStillReturnsAContextWithOneWarningPerPart()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var reader = new GsdFakeSubstrateReader { Respond = static (_, _) => throw new IOException("disk gone") };
        var source = new GsdTaskContextSource(NewClient(directory, "{ not json"), reader);

        var context = await source.GetAsync(GsdTestSupport.Scope(root), Token).ConfigureAwait(true);

        context.Title.ShouldBe("Untitled task");
        context.Phase.ShouldBeNull();
        context.Warnings.Count.ShouldBe(2);
        context.Warnings[0].ShouldStartWith("Workflow phase is unknown: ");
        context.Warnings[1].ShouldStartWith("Substrate context is unavailable: ");
    }

    [Fact]
    public async Task EachScopeGetsItsOwnWorkspacePhaseAndTheReaderSeesTheSameScope()
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
        var reader = new GsdFakeSubstrateReader();
        var source = new GsdTaskContextSource(GsdTestSupport.NewClient(directory, runner), reader);
        var firstScope = GsdTestSupport.Scope(first, "session-a");
        var secondScope = GsdTestSupport.Scope(second, "session-b");

        var one = await source.GetAsync(firstScope, Token).ConfigureAwait(true);
        var two = await source.GetAsync(secondScope, Token).ConfigureAwait(true);

        one.Phase.ShouldNotBeNull().Name.ShouldBe("Alpha");
        two.Phase.ShouldNotBeNull().Name.ShouldBe("Omega");
        runner.Calls.Select(call => call.ProjectDirectory).ShouldBe([first, second]);
        reader.Scopes.ShouldBe([firstScope, secondScope]);
    }

    [Fact]
    public async Task CancelledTokenThrowsCancellationAndReadsNothing()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        var runner = new GsdFakeProcessRunner();
        var reader = new GsdFakeSubstrateReader();
        var source = new GsdTaskContextSource(GsdTestSupport.NewClient(directory, runner), reader);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(() => source.GetAsync(GsdTestSupport.Scope(root), cancellation.Token)).ConfigureAwait(true);

        runner.CallCount.ShouldBe(0);
        reader.Scopes.ShouldBeEmpty();
    }

    [Fact]
    public async Task CancellationDuringTheSubstrateReadPropagatesInsteadOfBecomingAWarning()
    {
        using var directory = new GsdTempDirectory();
        var root = directory.CreateWorkspace("ws");
        using var cancellation = new CancellationTokenSource();
        var reader = new GsdFakeSubstrateReader
        {
            Respond = (_, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return new SubstrateContextSnapshot { SharedMemory = [], Lessons = [], Warnings = [] };
            },
        };
        var source = new GsdTaskContextSource(NewClient(directory, GsdFixtures.Load("roadmap-analyze.json")), reader);

        await Should.ThrowAsync<OperationCanceledException>(() => source.GetAsync(GsdTestSupport.Scope(root), cancellation.Token)).ConfigureAwait(true);
    }

    private static GsdToolsClient NewClient(GsdTempDirectory directory, string output) =>
        GsdTestSupport.NewClient(directory, new GsdFakeProcessRunner { Respond = _ => output });
}
