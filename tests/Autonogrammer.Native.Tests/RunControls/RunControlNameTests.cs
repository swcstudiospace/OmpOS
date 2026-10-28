using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Autonogrammer.Presentation.Approvals;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.RunControls;

/// <summary>Accessible names and keyboard reach of the run controls (UI-SPEC accessibility; criterion 4).</summary>
public sealed class RunControlNameTests
{
    /// <summary>Every new control that keyboard focus reaches, and every live region, has an accessible name.</summary>
    [Fact]
    public async Task EveryNewTabStopAndLiveRegionHasAName()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = RunControlsHarness.Show(new ScriptedApprovalBridge());
            ShellHarness.RuntimeOf(window).Runs.RunStarted();
            RunControlsHarness.AddRequest(window, "request-1");

            var unnamed = window.GetVisualDescendants()
                .OfType<Control>()
                .Where(static control => control.TemplatedParent is null)
                .Where(static control => (control.Focusable && control.IsTabStop)
                    || AutomationProperties.GetLiveSetting(control) is AutomationLiveSetting.Polite or AutomationLiveSetting.Assertive)
                .Where(static control => string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)))
                .Select(static control => control.GetType().Name)
                .ToList();

            unnamed.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Each approval control, the steer and follow-up controls, and the pending label carry their names.</summary>
    [Fact]
    public async Task TheRunControlsCarryTheirNames()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = RunControlsHarness.Show(new ScriptedApprovalBridge());
            RunControlsHarness.AddRequest(window, "request-1");
            ShellHarness.RuntimeOf(window).Runs.RunStarted();
            ShellHarness.Settle(window);

            AutomationProperties.GetName(RunControlsHarness.Named(window, "Approve tool request")).ShouldBe("Approve tool request");
            AutomationProperties.GetName(RunControlsHarness.Named(window, "Deny tool request")).ShouldBe("Deny tool request");
            AutomationProperties.GetName(ShellHarness.Send(window)).ShouldBe("Steer message");
            AutomationProperties.GetName(RunControlsHarness.FollowUp(window)).ShouldBe("Queue follow-up message");
            AutomationProperties.GetName(RunControlsHarness.PendingLabel(window)).ShouldBe("Pending input");
            AutomationProperties.GetName(RunControlsHarness.RunControlsList(window).FindControl<ItemsControl>("ApprovalItems")!).ShouldBe("Pending tool requests");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Tab reaches Approve, then Deny, then the prompt, in that order.</summary>
    [Fact]
    public async Task TabReachesApproveThenDenyThenThePrompt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = RunControlsHarness.Show(new ScriptedApprovalBridge());
            RunControlsHarness.AddRequest(window, "request-1");

            RunControlsHarness.Named(window, "Approve tool request").Focus();
            ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            RunControlsHarness.Named(window, "Deny tool request").IsFocused.ShouldBeTrue("deny follows approve");

            ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            ShellHarness.Prompt(window).IsFocused.ShouldBeTrue("the prompt follows the approval rows");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter on a focused Approve control sends the decision, so the control is keyboard operable.</summary>
    [Fact]
    public async Task EnterOnTheApproveControlSendsTheDecision()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var bridge = new ScriptedApprovalBridge(ToolApprovalOutcome.Accepted);
            var window = RunControlsHarness.Show(bridge);
            var approval = RunControlsHarness.AddRequest(window, "request-1");

            RunControlsHarness.Named(window, "Approve tool request").Focus();
            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the keyboard decision", TestContext.Current.CancellationToken).ConfigureAwait(true);

            approval.Status.ShouldBe(ToolApprovalStatus.Allowed);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
