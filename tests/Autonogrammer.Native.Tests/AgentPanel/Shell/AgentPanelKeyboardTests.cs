using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Tests.Parity;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>The keyboard map of the UI spec, driven through the window the way the keyboard does, and the focus rules that go with it (AL-11).</summary>
public sealed class AgentPanelKeyboardTests
{
    private static readonly ScriptedTurn Working = new([new TextDeltaEvent("working")], HoldUntilCancelled: true);

    /// <summary>Ctrl+N creates an agent, selects it and moves focus to its prompt, from anywhere.</summary>
    [Fact]
    public async Task CtrlNCreatesAnAgentSelectsItAndFocusesItsPrompt()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            ShellHarness.ShowToolsView(window);
            ShellHarness.MainViewOf(window).FindControl<ToggleButton>("AgentsToggle")!.Focus();

            ShellHarness.Press(window, Key.N, PhysicalKey.N, control: true);
            await ShellWait.UntilAsync(window, () => string.Equals(fixture.Panel.Selected?.Id, "new-1", StringComparison.Ordinal), "the new agent to be selected", token).ConfigureAwait(true);

            fixture.Host.Runtimes.ShouldContainKey("new-1");
            ShellHarness.Composer(window).DataContext.ShouldBeSameAs(fixture.Runtime("new-1").Composer);
            ShellHarness.Prompt(window).IsFocused.ShouldBeTrue("the new agent's prompt has focus");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Ctrl+1 focuses the rail: the selected row in the Sessions view, the first tool toggle in the Tools view.</summary>
    [Fact]
    public async Task CtrlOneFocusesTheRail()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            var window = ShellHarness.Show(fixture);
            ShellHarness.Prompt(window).Focus();

            ShellHarness.Press(window, Key.D1, PhysicalKey.Digit1, control: true);

            FocusIn(window, "RailRegion").ShouldBeTrue("Ctrl+1 moves focus into the rail");
            ShellTree.NamedVisible<ListBox>(ShellHarness.MainViewOf(window), "AgentRows").ShouldNotBeNull();

            ShellHarness.ShowToolsView(window);
            ShellHarness.Prompt(window).Focus();
            ShellHarness.Press(window, Key.D1, PhysicalKey.Digit1, control: true);

            ShellHarness.MainViewOf(window).FindControl<ToggleButton>("AgentsToggle")!.IsFocused.ShouldBeTrue("in the Tools view Ctrl+1 focuses the first tool toggle");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Ctrl+2 focuses the prompt and closes an open tool.</summary>
    [Fact]
    public async Task CtrlTwoFocusesThePromptAndClosesAnOpenTool()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            ShellHarness.ShowToolsView(window);
            main.FindControl<ToggleButton>("EditorToggle")!.IsChecked = true;
            ShellHarness.Settle(window);
            main.FindControl<ContentControl>("ToolHost")!.IsVisible.ShouldBeTrue();

            ShellHarness.Press(window, Key.D2, PhysicalKey.Digit2, control: true);

            main.FindControl<ContentControl>("ToolHost")!.IsVisible.ShouldBeFalse("Ctrl+2 closes the open tool");
            main.FindControl<ContentControl>("TranscriptHost")!.IsVisible.ShouldBeTrue();
            main.FindControl<ToggleButton>("EditorToggle")!.IsChecked.ShouldBe(false);
            ShellHarness.Prompt(window).IsFocused.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Ctrl+3 and Ctrl+Shift+I are not assigned: the panel has no inspector, so neither key opens a region or moves focus.</summary>
    [Fact]
    public async Task CtrlThreeAndCtrlShiftIOpenNothingAndKeepFocus()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            var window = ShellHarness.Show(fixture, width: 960, height: 640);
            var main = ShellHarness.MainViewOf(window);
            ShellHarness.Prompt(window).Focus();

            ShellHarness.Press(window, Key.D3, PhysicalKey.Digit3, control: true);
            ShellHarness.Press(window, Key.I, PhysicalKey.I, shift: true, control: true);

            ShellHarness.Prompt(window).IsFocused.ShouldBeTrue("focus stays in the prompt");
            main.FindControl<Grid>("RegionGrid")!.Children.Count(static child => ((Control)child).IsVisible).ShouldBe(2, "no region opens beside the rail and the thread");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>F6 cycles the regions rail, thread and back; Shift+F6 goes the other way.</summary>
    [Fact]
    public async Task F6CyclesTheRegions()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            ShellHarness.Prompt(window).Focus();

            ShellHarness.Press(window, Key.F6, PhysicalKey.F6);
            FocusIn(window, "RailRegion").ShouldBeTrue("F6 from the thread goes to the rail");
            ShellHarness.Press(window, Key.F6, PhysicalKey.F6);
            FocusIn(window, "ThreadRegion").ShouldBeTrue("F6 from the rail goes to the thread");
            ShellHarness.Prompt(window).IsFocused.ShouldBeTrue("the thread region lands on the prompt");

            ShellHarness.Press(window, Key.F6, PhysicalKey.F6, shift: true);
            FocusIn(window, "RailRegion").ShouldBeTrue("Shift+F6 from the thread goes back to the rail");
            ShellHarness.Press(window, Key.F6, PhysicalKey.F6, shift: true);
            FocusIn(window, "ThreadRegion").ShouldBeTrue("Shift+F6 from the rail wraps to the thread");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter on the focused pipeline strip opens the stage rows flyout, and the flyout holds a keyboard stop of its own.</summary>
    [Fact]
    public async Task EnterOnThePipelineStripOpensTheStageRowsFlyout()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            var strip = main.FindControl<ContentControl>("PipelineHost")!.Content.ShouldBeOfType<PipelineStripView>();
            var button = strip.FindControl<Button>("PipelineStrip")!;
            button.Focus();

            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);

            ((Flyout)button.Flyout!).IsOpen.ShouldBeTrue("Enter opens the stage rows flyout");
            strip.FindControl<PipelineTabView>("Details")!.FindControl<ScrollViewer>("PipelineDetails")!.IsTabStop.ShouldBeTrue("the stage rows are one tab stop");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Ctrl+F opens the transcript search and focuses its box; Esc closes it and returns focus to the transcript.</summary>
    [Fact]
    public async Task CtrlFOpensTheTranscriptSearchAndEscapeClosesIt()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            var searchHost = main.FindControl<ContentControl>("SearchHost")!;
            searchHost.IsVisible.ShouldBeFalse("the transcript search is closed until it is asked for");
            ShellHarness.Prompt(window).Focus();

            ShellHarness.Press(window, Key.F, PhysicalKey.F, control: true);

            searchHost.IsVisible.ShouldBeTrue();
            ShellTree.Named<TextBox>(searchHost, "SearchInput")!.IsFocused.ShouldBeTrue("the search box has focus");

            ShellHarness.Press(window, Key.Escape, PhysicalKey.Escape);

            searchHost.IsVisible.ShouldBeFalse("Esc dismisses the search");
            ShellHarness.Scroll(window).IsFocused.ShouldBeTrue("focus returns to the transcript");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Esc in the prompt while a run is active asks first; Esc or Keep running dismisses and returns focus to the prompt, and Stop agent stops.</summary>
    [Fact]
    public async Task EscapeWhileARunIsActiveAsksBeforeStopping()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession(SessionScript.WithTurns(Working));
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            var runtime = fixture.Runtime("session-1");
            var channel = fixture.Script("session-1").Scripted;
            await SendAsync(window, "long task").ConfigureAwait(true);

            ShellHarness.Press(window, Key.Escape, PhysicalKey.Escape);

            ShellTree.NamedVisible<Border>(main, "StopConfirmBar").ShouldNotBeNull("Esc opens the stop confirmation bar");
            ShellTree.NamedVisible<Button>(main, "StopAgentButton")!.IsFocused.ShouldBeTrue("Stop agent has focus when the bar opens");
            channel.CancelCount.ShouldBe(0, "nothing stopped yet");
            AutomationProperties.GetLiveSetting(ShellTree.NamedVisible<Border>(main, "StopConfirmBar")!).ShouldBe(AutomationLiveSetting.Polite);

            ShellHarness.Press(window, Key.Escape, PhysicalKey.Escape);

            ShellTree.NamedVisible<Border>(main, "StopConfirmBar").ShouldBeNull("Esc dismisses the bar");
            ShellHarness.Prompt(window).IsFocused.ShouldBeTrue("focus returns to the prompt");
            channel.CancelCount.ShouldBe(0);
            runtime.Composer.IsStreaming.ShouldBeTrue();

            ShellHarness.Press(window, Key.Escape, PhysicalKey.Escape);
            ShellHarness.Click(window, ShellTree.NamedVisible<Button>(main, "KeepRunningButton")!);

            ShellTree.NamedVisible<Border>(main, "StopConfirmBar").ShouldBeNull("Keep running dismisses the bar");
            ShellHarness.Prompt(window).IsFocused.ShouldBeTrue();
            channel.CancelCount.ShouldBe(0);

            ShellHarness.Press(window, Key.Escape, PhysicalKey.Escape);
            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await ShellWait.RunEndedAsync(window, runtime, token).ConfigureAwait(true);

            channel.CancelCount.ShouldBe(1, "Enter on Stop agent stops the run");
            ShellTree.NamedVisible<Border>(main, "StopConfirmBar").ShouldBeNull("the bar closes when the run ends");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With nothing running, Esc in the prompt opens nothing.</summary>
    [Fact]
    public async Task EscapeWithNothingRunningOpensNothing()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            ShellHarness.Prompt(window).Focus();

            ShellHarness.Press(window, Key.Escape, PhysicalKey.Escape);

            ShellTree.NamedVisible<Border>(main, "StopConfirmBar").ShouldBeNull();
            ShellHarness.Prompt(window).IsFocused.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The Stop button stops at once, with no confirmation, and the partial output stays.</summary>
    [Fact]
    public async Task TheStopButtonStopsAtOnce()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession(SessionScript.WithTurns(Working));
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var runtime = fixture.Runtime("session-1");
            await SendAsync(window, "long task").ConfigureAwait(true);

            ShellHarness.Click(window, ShellHarness.Stop(window));
            await ShellWait.RunEndedAsync(window, runtime, token).ConfigureAwait(true);

            fixture.Script("session-1").Scripted.CancelCount.ShouldBe(1);
            ShellTree.NamedVisible<Border>(ShellHarness.MainViewOf(window), "StopConfirmBar").ShouldBeNull("the button asks nothing");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>While a run is active Ctrl+Enter steers and Ctrl+Shift+Enter queues a follow-up; both enter the steering history.</summary>
    [Fact]
    public async Task CtrlEnterSteersAndCtrlShiftEnterQueuesAFollowUp()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession(SessionScript.WithTurns(Working, new ScriptedTurn([new CompletedEvent("end_turn")])));
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var runtime = fixture.Runtime("session-1");
            await SendAsync(window, "long task").ConfigureAwait(true);

            ShellHarness.Prompt(window).Text = "turn left";
            ShellHarness.Prompt(window).Focus();
            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter, control: true);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Prompt(window).Text = "then summarise";
            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter, shift: true, control: true);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            var history = runtime.ViewModel.Steering.History;
            history.Select(entry => (entry.Mode, entry.Text)).ShouldBe(
                [(SteeringMode.FollowUp, "then summarise"), (SteeringMode.Steer, "turn left")],
                "newest first");
            ShellHarness.Prompt(window).Text.ShouldBeNullOrEmpty("the draft clears once the message is accepted");

            await runtime.Composer.StopAsync().ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, runtime, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter in the agent list focuses the selected agent's prompt.</summary>
    [Fact]
    public async Task EnterInTheAgentListFocusesThePrompt()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            ShellHarness.Press(window, Key.D1, PhysicalKey.Digit1, control: true);

            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);

            ShellHarness.Prompt(window).IsFocused.ShouldBeTrue("Enter in the list focuses the prompt of the selected agent");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Delete on a running agent's row asks before closing it; on an idle agent's row it closes at once.</summary>
    [Fact]
    public async Task DeleteOnARunningRowAsksFirst()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession(SessionScript.WithTurns(Working));
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("session-2", "Agent two");
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            var runtime = fixture.Runtime("session-1");
            await SendAsync(window, "long task").ConfigureAwait(true);

            ShellHarness.Press(window, Key.D1, PhysicalKey.Digit1, control: true);
            ShellHarness.Press(window, Key.Delete, PhysicalKey.Delete);

            ShellTree.NamedVisible<Border>(main, "CloseConfirmBar").ShouldNotBeNull("Delete on a running row asks first");
            fixture.Host.Closed.ShouldBeEmpty();

            ShellHarness.Press(window, Key.Escape, PhysicalKey.Escape);
            ShellTree.NamedVisible<Border>(main, "CloseConfirmBar").ShouldBeNull("Esc dismisses the close confirmation");
            ShellTree.NamedVisible<Button>(main, "CloseButton")!.IsFocused.ShouldBeTrue("focus returns to Close this agent");

            await runtime.Composer.StopAsync().ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, runtime, token).ConfigureAwait(true);
            fixture.Panel.SelectSession("session-2");
            ShellHarness.Settle(window);
            ShellHarness.Press(window, Key.D1, PhysicalKey.Digit1, control: true);
            ShellHarness.Press(window, Key.Delete, PhysicalKey.Delete);
            await ShellWait.UntilAsync(window, () => fixture.Host.Closed.Count == 1, "the idle agent to close", token).ConfigureAwait(true);

            fixture.Host.Closed.ShouldBe(["session-2"], "an idle agent closes at once");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static bool FocusIn(MainWindow window, string regionName)
    {
        var focused = window.FocusManager?.GetFocusedElement() as Visual;
        var region = ShellHarness.MainViewOf(window).FindControl<Control>(regionName)!;
        return focused is not null && (ReferenceEquals(focused, region) || region.IsVisualAncestorOf(focused));
    }

    private static async Task SendAsync(MainWindow window, string text)
    {
        ShellHarness.Prompt(window).Text = text;
        ShellHarness.Prompt(window).Focus();
        ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
        await ShellHarness.DrainAsync(window).ConfigureAwait(true);
    }
}
