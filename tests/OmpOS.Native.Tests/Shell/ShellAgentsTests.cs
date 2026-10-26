using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using OmpOS.Native.Views.Agents;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Shell;

/// <summary>
/// The Agents window composed into the shell (ADE-05): reachable from the keyboard, opened in place of the transcript, and
/// bound to the one registry and the one view model the main window owns.
/// </summary>
public sealed class ShellAgentsTests
{
    /// <summary>The Agents toggle is a named tab stop that follows the transcript search, and Enter opens and closes the Agents view.</summary>
    [Fact]
    public async Task TheAgentsViewIsReachableFromTheShell()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            var toggle = AgentsToggle(window);
            AutomationProperties.GetName(toggle).ShouldBe("Agents");

            ShellHarness.Stop(window).Focus();
            ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            SearchInput(window).IsFocused.ShouldBeTrue("the transcript search follows Stop");

            ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            toggle.IsFocused.ShouldBeTrue("the Agents toggle follows the transcript search");

            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);

            toggle.IsChecked.ShouldBe(true);
            main.FindControl<ContentControl>("AgentsHost")!.IsVisible.ShouldBeTrue();
            main.FindControl<ContentControl>("TranscriptHost")!.IsVisible.ShouldBeFalse("the Agents view stands in for the transcript");

            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);

            toggle.IsChecked.ShouldBe(false);
            main.FindControl<ContentControl>("AgentsHost")!.IsVisible.ShouldBeFalse();
            main.FindControl<ContentControl>("TranscriptHost")!.IsVisible.ShouldBeTrue("closing the Agents view shows the transcript again");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The shell's Agents view is the view of the window's own view model, and it draws the window's own registry.</summary>
    [Fact]
    public async Task TheAgentsViewDrawsTheWindowsRegistryThroughTheWindowsModel()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            main.AgentRuns.Runs.ShouldBeEmpty();

            var build = main.AgentRuns.Add("build", "Build the app");
            AgentsToggle(window).IsChecked = true;
            ShellHarness.Settle(window);

            var view = main.FindControl<ContentControl>("AgentsHost")!.Content.ShouldBeOfType<AgentsView>();
            view.DataContext.ShouldBeSameAs(main.Agents);
            main.Agents.VisibleRuns.ShouldBe([build]);
            RowNames(view).ShouldBe(["Build the app, Running"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static ToggleButton AgentsToggle(MainWindow window) => ShellHarness.MainViewOf(window).FindControl<ToggleButton>("AgentsToggle")!;

    private static TextBox SearchInput(MainWindow window) =>
        ((Control)ShellHarness.MainViewOf(window).FindControl<ContentControl>("SearchHost")!.Content!).FindControl<TextBox>("SearchInput")!;

    private static string[] RowNames(AgentsView view) => view.GetVisualDescendants()
        .OfType<Button>()
        .Select(static button => AutomationProperties.GetName(button) ?? string.Empty)
        .Where(static name => name.Contains(',', StringComparison.Ordinal))
        .ToArray();
}
