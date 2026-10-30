using Autonogrammer.Presentation.Agents;
using Shouldly;
using Xunit;
using Status = Autonogrammer.Presentation.Agents.AgentRunStatus;

namespace Autonogrammer.Presentation.Tests.Agents;

/// <summary>
/// The Agents window state on <see cref="AgentsViewModel"/>: the status filter, and the open and close view actions that never
/// change a run's status.
/// </summary>
public sealed class AgentsViewModelTests
{
    /// <summary>Each filter shows exactly the runs with that status, in the order they were added; All shows every run.</summary>
    [Fact]
    public void FilterShowsOnlyTheRunsWithTheSelectedStatus()
    {
        var registry = new AgentRunRegistry();
        var model = new AgentsViewModel(registry);
        registry.Add("build", "Build the app");
        registry.Add("docs", "Update the docs");
        registry.Add("tests", "Run the tests");
        registry.Add("lint", "Lint the code");
        registry.Add("review", "Review the diff");
        registry.Transition("docs", Status.Completed);
        registry.Transition("tests", Status.Failed);
        registry.Transition("lint", Status.Cancelled);

        Titles(model).ShouldBe(["Build the app", "Update the docs", "Run the tests", "Lint the code", "Review the diff"]);

        model.StatusFilter = Status.Running;
        Titles(model).ShouldBe(["Build the app", "Review the diff"]);

        model.StatusFilter = Status.Completed;
        Titles(model).ShouldBe(["Update the docs"]);

        model.StatusFilter = Status.Failed;
        Titles(model).ShouldBe(["Run the tests"]);

        model.StatusFilter = Status.Cancelled;
        Titles(model).ShouldBe(["Lint the code"]);

        model.StatusFilter = null;
        Titles(model).ShouldBe(["Build the app", "Update the docs", "Run the tests", "Lint the code", "Review the diff"]);
    }

    /// <summary>A run that leaves the filtered status disappears from the filtered list, and a new run joins it when it matches.</summary>
    [Fact]
    public void FilteredListFollowsStatusTransitionsAndNewRuns()
    {
        var registry = new AgentRunRegistry();
        var model = new AgentsViewModel(registry);
        registry.Add("build", "Build the app");
        registry.Add("tests", "Run the tests");
        model.StatusFilter = Status.Running;

        registry.Transition("build", Status.Completed);
        Titles(model).ShouldBe(["Run the tests"]);

        registry.Add("review", "Review the diff");
        Titles(model).ShouldBe(["Run the tests", "Review the diff"]);
    }

    /// <summary>Opening a view shows that run, and closing it clears the view; neither changes the run's status.</summary>
    [Fact]
    public void OpenAndCloseChangeOnlyTheViewState()
    {
        var registry = new AgentRunRegistry();
        var model = new AgentsViewModel(registry);
        var build = registry.Add("build", "Build the app");
        var announced = PropertyChangeRecorder.Record(build);

        model.OpenRun(build);
        model.OpenedRun.ShouldBe(build);
        build.Status.ShouldBe(Status.Running);

        model.CloseRun();
        model.OpenedRun.ShouldBeNull();
        build.Status.ShouldBe(Status.Running);
        announced.ShouldBeEmpty();
    }

    /// <summary>Closing the view of a running run does not stop it: it can still complete afterwards.</summary>
    [Fact]
    public void ClosingTheViewDoesNotStopTheRun()
    {
        var registry = new AgentRunRegistry();
        var model = new AgentsViewModel(registry);
        var build = registry.Add("build", "Build the app");
        model.OpenRun(build);

        model.CloseRun();

        build.Status.ShouldBe(Status.Running);
        registry.Transition("build", Status.Completed);
        build.Status.ShouldBe(Status.Completed);
    }

    /// <summary>Closing the view of a finished run leaves its status as it was.</summary>
    [Fact]
    public void ClosingTheViewOfAFinishedRunKeepsItsStatus()
    {
        var registry = new AgentRunRegistry();
        var model = new AgentsViewModel(registry);
        var docs = registry.Add("docs", "Update the docs");
        registry.Transition("docs", Status.Failed);
        model.OpenRun(docs);

        model.CloseRun();

        docs.Status.ShouldBe(Status.Failed);
        model.OpenedRun.ShouldBeNull();
    }

    /// <summary>Only a run that belongs to the registry can be opened; a refused open leaves the view unchanged.</summary>
    [Fact]
    public void RunFromAnotherRegistryCannotBeOpened()
    {
        var model = new AgentsViewModel(new AgentRunRegistry());
        var foreign = new AgentRunRegistry().Add("build", "Build the app");

        Should.Throw<ArgumentException>(() => model.OpenRun(foreign));

        model.OpenedRun.ShouldBeNull();
    }

    /// <summary>The filter and the open run are announced when they change.</summary>
    [Fact]
    public void FilterAndOpenedRunAreAnnounced()
    {
        var registry = new AgentRunRegistry();
        var model = new AgentsViewModel(registry);
        var build = registry.Add("build", "Build the app");
        var announced = PropertyChangeRecorder.Record(model);

        model.StatusFilter = Status.Running;
        model.OpenRun(build);
        model.CloseRun();

        announced.ShouldAnnounce(nameof(AgentsViewModel.StatusFilter));
        announced.ShouldAnnounce(nameof(AgentsViewModel.OpenedRun));
    }

    private static List<string> Titles(AgentsViewModel model) => model.VisibleRuns.Select(run => run.Title).ToList();
}
