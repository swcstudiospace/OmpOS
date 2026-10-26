using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using OmpOS.Native.Tests.AgentPanel.Shell;
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
    /// <summary>
    /// The Agents toggle lives in the rail's Tools view, which follows the Sessions view toggle in the tab order. Inside the Tools view the
    /// toggle is reachable by Tab, and Enter opens and closes the Agents view.
    /// </summary>
    [Fact]
    public async Task TheAgentsViewIsReachableFromTheShell()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            var sessionsToggle = main.FindControl<ToggleButton>("SessionsViewToggle")!;
            var toolsToggle = main.FindControl<ToggleButton>("ToolsViewToggle")!;

            sessionsToggle.Focus();
            ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            toolsToggle.IsFocused.ShouldBeTrue("the Tools view toggle follows the Sessions view toggle");

            ShellHarness.ShowToolsView(window);
            var toggle = AgentsToggle(window);
            AutomationProperties.GetName(toggle).ShouldBe("Agents");

            toolsToggle.Focus();
            for (var presses = 0; presses < 20 && !toggle.IsFocused; presses++)
            {
                ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            }

            toggle.IsFocused.ShouldBeTrue("the Agents toggle is a tab stop inside the Tools view");

            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);

            toggle.IsChecked.ShouldBe(true);
            main.FindControl<ContentControl>("AgentsHost")!.IsVisible.ShouldBeTrue();
            main.FindControl<ContentControl>("TranscriptHost")!.IsVisible.ShouldBeFalse("the Agents view stands in for the transcript");
            ShellTree.Named<Border>(main, "ToolHeader")!.IsVisible.ShouldBeTrue("an open tool shows its header");

            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);

            toggle.IsChecked.ShouldBe(false);
            main.FindControl<ContentControl>("AgentsHost")!.IsVisible.ShouldBeFalse();
            main.FindControl<ContentControl>("TranscriptHost")!.IsVisible.ShouldBeTrue("closing the Agents view shows the transcript again");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// The Settings button is the last stop of the rail: Tab from the last tool toggle reaches it, and Enter on it asks for the Settings
    /// window. It follows the Tools view toggles in the tab order documented in MainView.axaml.
    /// </summary>
    [Fact]
    public async Task TheSettingsButtonIsTheLastTabStopOfTheRail()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            ShellHarness.ShowToolsView(window);
            var exportToggle = main.FindControl<ToggleButton>("ExportToggle")!;
            var settings = main.FindControl<Button>("SettingsButton")!;
            var requested = 0;
            main.SettingsRequested += (_, _) => requested++;

            exportToggle.Focus();
            ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);

            settings.IsFocused.ShouldBeTrue("the Settings button follows the last tool toggle");
            AutomationProperties.GetName(settings).ShouldBe("Settings");
            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            requested.ShouldBe(1);

            ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            settings.IsFocused.ShouldBeFalse("nothing in the rail follows the Settings button");
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
            ShellHarness.ShowToolsView(window);
            AgentsToggle(window).IsChecked = true;
            ShellHarness.Settle(window);

            var view = main.FindControl<ContentControl>("AgentsHost")!.Content.ShouldBeOfType<AgentsView>();
            view.DataContext.ShouldBeSameAs(main.Agents);
            main.Agents.VisibleRuns.ShouldBe([build]);
            RowNames(view).ShouldBe(["Build the app, Running"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static ToggleButton AgentsToggle(MainWindow window) => ShellHarness.MainViewOf(window).FindControl<ToggleButton>("AgentsToggle")!;

    private static string[] RowNames(AgentsView view) => view.GetVisualDescendants()
        .OfType<Button>()
        .Select(static button => AutomationProperties.GetName(button) ?? string.Empty)
        .Where(static name => name.Contains(',', StringComparison.Ordinal))
        .ToArray();
}
