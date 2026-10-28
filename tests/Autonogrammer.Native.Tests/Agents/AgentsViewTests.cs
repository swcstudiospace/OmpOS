using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Autonogrammer.Native.Views.Agents;
using Autonogrammer.Presentation.Agents;
using Shouldly;
using Xunit;
using Status = Autonogrammer.Presentation.Agents.AgentRunStatus;

namespace Autonogrammer.Native.Tests.Agents;

/// <summary>The Agents window (ADE-05) on the headless platform: the run list, the status filter, and opening and closing a run.</summary>
public sealed class AgentsViewTests
{
    /// <summary>The view lists each run by its title and status text, in the order the runs were added.</summary>
    [Fact]
    public async Task TheViewListsEachRunWithItsTitleAndStatus()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (_, model) = NewModel();
            var window = ShowAgents(model);
            var view = View(window);

            RowNames(view).ShouldBe(["Build the app, Running", "Update the docs, Completed", "Run the tests, Failed", "Lint the code, Cancelled"]);
            RowTexts(view, 2).ShouldBe(["Run the tests", "Failed"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Choosing a status in the filter shows only the runs with that status; All shows every run again.</summary>
    [Fact]
    public async Task TheStatusFilterShowsOnlyTheRunsWithThatStatus()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (_, model) = NewModel();
            var window = ShowAgents(model);
            var view = View(window);

            Filter(view).SelectedItem = "Running";
            ShellHarness.Settle(window);
            RowNames(view).ShouldBe(["Build the app, Running"]);
            model.StatusFilter.ShouldBe(Status.Running);

            Filter(view).SelectedItem = "Cancelled";
            ShellHarness.Settle(window);
            RowNames(view).ShouldBe(["Lint the code, Cancelled"]);

            Filter(view).SelectedItem = "All";
            ShellHarness.Settle(window);
            RowNames(view).Count.ShouldBe(4);
            model.StatusFilter.ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The filter follows the model's filter, and a status with no runs shows the empty copy until a run has it.</summary>
    [Fact]
    public async Task TheFilterFollowsTheModelAndAnEmptyFilterSaysSo()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (registry, model) = NewModel();
            var window = ShowAgents(model);
            var view = View(window);
            var empty = view.FindControl<TextBlock>("EmptyState")!;

            model.StatusFilter = Status.Failed;
            ShellHarness.Settle(window);
            Filter(view).SelectedItem.ShouldBe("Failed");
            RowNames(view).ShouldBe(["Run the tests, Failed"]);
            empty.IsVisible.ShouldBeFalse();

            model.StatusFilter = Status.Running;
            registry.Transition("build", Status.Completed);
            ShellHarness.Settle(window);
            RowNames(view).ShouldBeEmpty();
            empty.IsVisible.ShouldBeTrue();
            empty.Text.ShouldBe("No runs have this status.");

            model.StatusFilter = null;
            ShellHarness.Settle(window);
            Filter(view).SelectedItem.ShouldBe("All");
            empty.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A row click opens the run's view with its title, status and transcript; Close run closes it.</summary>
    [Fact]
    public async Task ClickingARowOpensItsViewAndCloseRunClosesIt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (registry, model) = NewModel();
            var build = registry.Runs[0];
            build.AppendTranscript("build: compiling");
            var window = ShowAgents(model);
            var view = View(window);
            var panel = view.FindControl<Border>("RunPanel")!;
            panel.IsVisible.ShouldBeFalse();

            Click(Row(view, "Build the app, Running"));
            ShellHarness.Settle(window);

            model.OpenedRun.ShouldBe(build);
            panel.IsVisible.ShouldBeTrue();
            PanelTexts(panel).ShouldBe(["Build the app", "Running", "build: compiling"]);

            Click(view.FindControl<Button>("CloseRunButton")!);
            ShellHarness.Settle(window);

            model.OpenedRun.ShouldBeNull();
            panel.IsVisible.ShouldBeFalse();
            build.Status.ShouldBe(Status.Running);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Closing the view does not stop the run: its row keeps following the run's status.</summary>
    [Fact]
    public async Task ClosingTheViewLeavesTheRunGoingAndItsRowUpdates()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (registry, model) = NewModel();
            var window = ShowAgents(model);
            var view = View(window);
            Click(Row(view, "Build the app, Running"));
            Click(view.FindControl<Button>("CloseRunButton")!);
            ShellHarness.Settle(window);

            registry.Transition("build", Status.Completed);
            ShellHarness.Settle(window);

            AutomationProperties.GetName(Row(view, "Build the app, Completed")).ShouldBe("Build the app, Completed");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Every control the view exposes to keyboard focus has an accessible name.</summary>
    [Fact]
    public async Task EveryFocusableControlOfTheViewHasAName()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (_, model) = NewModel();
            var window = ShowAgents(model);
            var view = View(window);
            Click(Row(view, "Build the app, Running"));
            ShellHarness.Settle(window);

            var unnamed = view.GetVisualDescendants()
                .OfType<Control>()
                .Where(static control => control.TemplatedParent is null)
                .Where(static control => control.Focusable && control.IsTabStop)
                .Where(static control => string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)))
                .Select(static control => control.GetType().Name)
                .ToList();

            unnamed.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static (AgentRunRegistry Registry, AgentsViewModel Model) NewModel()
    {
        var registry = new AgentRunRegistry();
        registry.Add("build", "Build the app");
        registry.Add("docs", "Update the docs");
        registry.Add("tests", "Run the tests");
        registry.Add("lint", "Lint the code");
        registry.Transition("docs", Status.Completed);
        registry.Transition("tests", Status.Failed);
        registry.Transition("lint", Status.Cancelled);
        return (registry, new AgentsViewModel(registry));
    }

    private static Window ShowAgents(AgentsViewModel model)
    {
        var window = new Window { Content = new AgentsView { DataContext = model } };
        window.Show();
        ShellHarness.Settle(window);
        return window;
    }

    private static AgentsView View(Window window) => (AgentsView)window.Content!;

    private static ComboBox Filter(AgentsView view) => view.FindControl<ComboBox>("StatusFilter")!;

    private static List<Button> Rows(AgentsView view) =>
        view.FindControl<ItemsControl>("AgentRows")!.GetVisualDescendants().OfType<Button>().ToList();

    private static List<string> RowNames(AgentsView view) => Rows(view).Select(button => AutomationProperties.GetName(button) ?? string.Empty).ToList();

    private static Button Row(AgentsView view, string name) =>
        Rows(view).Single(button => string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));

    private static List<string> RowTexts(AgentsView view, int index) =>
        Rows(view)[index].GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty).ToList();

    private static List<string> PanelTexts(Border panel) =>
        panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(text => !text.GetVisualAncestors().OfType<Button>().Any())
            .Select(text => text.Text ?? string.Empty)
            .ToList();

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
