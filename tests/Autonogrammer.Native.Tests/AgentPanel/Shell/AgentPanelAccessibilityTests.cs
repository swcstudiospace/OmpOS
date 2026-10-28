using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Tests.Parity;
using Autonogrammer.Native.Views.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>The accessibility walk over the opened Agent Panel: every tab stop and live region has a name, and the tab order is the order of the Control Inventory (AL-11).</summary>
public sealed class AgentPanelAccessibilityTests
{
    private const int TabLimit = 60;

    private static readonly ScriptedTurn Working = new([new TextDeltaEvent("working")], HoldUntilCancelled: true);

    /// <summary>With a session selected, every authored tab stop and live region has a non-empty name, in the panel and in the stage rows flyout of the strip.</summary>
    [Fact]
    public async Task EveryAuthoredTabStopAndLiveRegionHasANameWithASessionSelected()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            var window = ShellHarness.Show(fixture);

            Unnamed(window).ShouldBeEmpty();

            var strip = ShellHarness.MainViewOf(window).FindControl<ContentControl>("PipelineHost")!.Content.ShouldBeOfType<PipelineStripView>();
            strip.ShowDetails();
            ShellHarness.Settle(window);
            var details = strip.FindControl<PipelineTabView>("Details").ShouldNotBeNull("the strip hosts the stage rows in its flyout");
            Unnamed(details).ShouldBeEmpty("the stage rows flyout");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The same walk over the Tools view of the rail with a tool open and its header showing.</summary>
    [Fact]
    public async Task EveryAuthoredTabStopHasANameInTheToolsViewWithAToolOpen()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            ShellHarness.ShowToolsView(window);
            Unnamed(window).ShouldBeEmpty("the Tools view");

            main.FindControl<ToggleButton>("ExplorerToggle")!.IsChecked = true;
            main.FindControl<ToggleButton>("SideChatsToggle")!.IsChecked = true;
            ShellHarness.Settle(window);

            Unnamed(window).ShouldBeEmpty("the Tools view with the explorer and the side chats open");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The same walk with the transcript search open, a run active, the stop confirmation open and a steering entry in the history.</summary>
    [Fact]
    public async Task EveryAuthoredTabStopHasANameWhileARunIsActiveWithTheBarsAndSearchOpen()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var script = new SessionScript
            {
                Prompts = new ScriptedPromptChannel(Working),
                RecordSteering = static (_, _) => Task.CompletedTask,
            };
            var fixture = ShellFixture.CreateWithSession(script);
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var runtime = fixture.Runtime("session-1");
            ShellHarness.Prompt(window).Text = "long task";
            ShellHarness.Prompt(window).Focus();
            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);
            await ShellWait.UntilAsync(window, () => runtime.Composer.IsStreaming, "the run to start", token).ConfigureAwait(true);
            ShellHarness.Prompt(window).Text = "turn left";
            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter, control: true);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);
            ShellHarness.Press(window, Key.F, PhysicalKey.F, control: true);
            ShellHarness.Press(window, Key.Escape, PhysicalKey.Escape);
            ShellHarness.Prompt(window).Focus();
            ShellHarness.Press(window, Key.Escape, PhysicalKey.Escape);
            ShellHarness.Press(window, Key.F, PhysicalKey.F, control: true);

            ShellTree.NamedVisible<Border>(ShellHarness.MainViewOf(window), "StopConfirmBar").ShouldNotBeNull();
            ShellTree.NamedVisible<Control>(ShellHarness.MainViewOf(window), "SearchInput").ShouldNotBeNull();
            Unnamed(window).ShouldBeEmpty();

            ShellHarness.Press(window, Key.Escape, PhysicalKey.Escape);
            await runtime.Composer.StopAsync().ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, runtime, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The same walk over the empty panel, over a lost agent and over the narrow layout.</summary>
    [Fact]
    public async Task EveryAuthoredTabStopHasANameInTheEmptyLostAndNarrowLayouts()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var empty = ShellHarness.Show(ShellFixture.Create());
            Unnamed(empty).ShouldBeEmpty("the empty panel");

            var lostFixture = ShellFixture.Create();
            lostFixture.AddSession("lost", "Lost agent", SessionLifecycle.Lost);
            lostFixture.Panel.SelectSession("lost");
            Unnamed(ShellHarness.Show(lostFixture)).ShouldBeEmpty("a lost agent");

            var narrow = ShellHarness.Show(ShellFixture.CreateWithSession(), width: 960, height: 640);
            Unnamed(narrow).ShouldBeEmpty("the narrow layout");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The live regions of the panel are the ones the UI spec lists, with the polite and assertive settings it gives them.</summary>
    [Fact]
    public async Task TheLiveRegionsCarryTheSpecSettings()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);

            var regions = main.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => AutomationProperties.GetLiveSetting(control) is AutomationLiveSetting.Polite or AutomationLiveSetting.Assertive)
                .ToDictionary(
                    control => AutomationProperties.GetName(control) ?? string.Empty,
                    control => AutomationProperties.GetLiveSetting(control),
                    StringComparer.Ordinal);

            regions["Pipeline announcements"].ShouldBe(AutomationLiveSetting.Polite);
            regions["Session status"].ShouldBe(AutomationLiveSetting.Polite);
            regions["Engine error"].ShouldBe(AutomationLiveSetting.Assertive);
            regions["Session announcements"].ShouldBe(AutomationLiveSetting.Polite);
            regions["Session notice"].ShouldBe(AutomationLiveSetting.Polite);
            regions["Pending input"].ShouldBe(AutomationLiveSetting.Polite);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Tab walks the controls of the Control Inventory in order, and every stop it visits has a name.</summary>
    [Fact]
    public async Task TabWalksTheControlInventoryInOrder()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            ShellHarness.SkipLink(window).Focus();

            var visited = new List<string>();
            for (var step = 0; step < TabLimit; step++)
            {
                ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
                var name = NameOfFocused(window);
                visited.Add(name);
                if (string.Equals(name, "Skip to conversation", StringComparison.Ordinal))
                {
                    break;
                }
            }

            visited.Count(name => name.Length == 0).ShouldBeLessThanOrEqualTo(1, "every tab stop has a name; one empty entry is the focus leaving the window before it wraps");
            AssertInOrder(
                visited,
                [
                    "Sessions view",
                    "Tools view",
                    "New agent",
                    "Filter agents",
                    "Agent sessions",
                    "Close this agent",
                    "Pipeline:",
                    "Session transcript",
                    "Message",
                    "Send message",
                    "Stop response",
                ]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static void AssertInOrder(List<string> visited, string[] expected)
    {
        var from = 0;
        foreach (var name in expected)
        {
            var at = visited.FindIndex(from, candidate => candidate.StartsWith(name, StringComparison.Ordinal));
            at.ShouldBeGreaterThanOrEqualTo(0, $"'{name}' must follow the previous stop in the tab order; visited: {string.Join(" | ", visited)}");
            from = at + 1;
        }
    }

    private static string NameOfFocused(MainWindow window) =>
        window.FocusManager?.GetFocusedElement() is Control focused ? AutomationProperties.GetName(focused) ?? string.Empty : string.Empty;

    private static List<string> Unnamed(Control root) =>
        [.. root.GetVisualDescendants()
            .OfType<Control>()
            .Where(static control => control.TemplatedParent is null && control.IsEffectivelyVisible)
            .Where(static control => (control.Focusable && control.IsTabStop)
                || AutomationProperties.GetLiveSetting(control) is AutomationLiveSetting.Polite or AutomationLiveSetting.Assertive)
            .Where(static control => string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)))
            .Select(static control => $"{control.GetType().Name} {control.Name}".Trim())];
}
