using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Runs;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Views;

/// <summary>The inline stop and close confirmation bars (46-UI-SPEC, Composer Behaviour and Control Inventory 29 and 30).</summary>
public sealed class ConfirmBarViewTests
{
    private static ConfirmBarView Open(out Window window, out AgentSessionViewModel model, out RunInputScheduler scheduler, out List<ConfirmBarKind> dismissed, string title = "Build the rail")
    {
        var clock = new ManualTimeProvider();
        var row = PanelFixtures.Row("s1", SessionLifecycle.Running, clock, title: title);
        scheduler = PanelFixtures.ActiveScheduler(new ScriptedRunInput());
        model = PanelFixtures.Session(row, clock, scheduler: scheduler);
        var bar = new ConfirmBarView { DataContext = model };
        var seen = new List<ConfirmBarKind>();
        bar.Dismissed += (_, args) => seen.Add(args.Kind);
        dismissed = seen;
        window = ViewHarness.Show(bar);
        return bar;
    }

    /// <summary>Both bars are closed at first, so neither is a tab stop.</summary>
    [Fact]
    public async Task BothBarsStartClosed()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var bar = Open(out var window, out var model, out _, out _);

            bar.IsStopOpen.ShouldBeFalse();
            bar.IsCloseOpen.ShouldBeFalse();
            ViewHarness.TabOrder(bar).ShouldBeEmpty();
            model.Dispose();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The stop bar shows the copy, names its region and buttons, and focuses Stop agent; Stop agent confirms and closes the bar.</summary>
    [Fact]
    public async Task TheStopBarAsksAndConfirms()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var bar = Open(out var window, out var model, out _, out var dismissed);
            var confirmed = 0;
            bar.StopConfirmed += (_, _) => confirmed++;

            bar.OpenStop();
            ViewHarness.Settle(window);

            bar.IsStopOpen.ShouldBeTrue();
            bar.IsCloseOpen.ShouldBeFalse();
            var region = ViewHarness.Find<Border>(bar, "StopConfirmBar");
            AutomationProperties.GetName(region).ShouldBe("Confirm stop");
            AutomationProperties.GetLiveSetting(region).ShouldBe(AutomationLiveSetting.Polite);
            ViewHarness.VisibleTexts(bar).ShouldContain(AgentPanelCopy.StopConfirmation, StringComparer.Ordinal);
            ViewHarness.TabOrder(bar).ShouldBe(["Stop agent", "Keep running"]);
            ViewHarness.Find<Button>(bar, "StopAgentButton").IsFocused.ShouldBeTrue("Stop agent takes focus when the bar opens");
            ViewHarness.Find<Button>(bar, "StopAgentButton").MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            ViewHarness.Find<Button>(bar, "KeepRunningButton").MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);

            ViewHarness.Press(window, Key.Enter, PhysicalKey.Enter);

            confirmed.ShouldBe(1);
            bar.IsStopOpen.ShouldBeFalse();
            dismissed.ShouldBeEmpty("confirming is not dismissing");
            model.Dispose();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Keep running and Esc dismiss the stop bar and tell the owner, so it can return focus to the prompt.</summary>
    [Fact]
    public async Task KeepRunningAndEscDismissTheStopBar()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var bar = Open(out var window, out var model, out _, out var dismissed);
            var confirmed = 0;
            bar.StopConfirmed += (_, _) => confirmed++;

            bar.OpenStop();
            ViewHarness.Settle(window);
            ViewHarness.Click(window, ViewHarness.Find<Button>(bar, "KeepRunningButton"));
            bar.IsStopOpen.ShouldBeFalse();

            bar.OpenStop();
            ViewHarness.Settle(window);
            ViewHarness.Press(window, Key.Escape, PhysicalKey.Escape);
            bar.IsStopOpen.ShouldBeFalse();

            dismissed.ShouldBe(new[] { ConfirmBarKind.Stop, ConfirmBarKind.Stop });
            confirmed.ShouldBe(0);
            model.Dispose();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The stop bar closes by itself when the run ends, and tells the owner.</summary>
    [Fact]
    public async Task TheStopBarClosesWhenTheRunEnds()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var bar = Open(out var window, out var model, out var scheduler, out var dismissed);

            bar.OpenStop();
            ViewHarness.Settle(window);
            await scheduler.RunCompletedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            ViewHarness.Settle(window);

            bar.IsStopOpen.ShouldBeFalse();
            dismissed.ShouldBe(new[] { ConfirmBarKind.Stop });
            model.Dispose();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The close bar names the agent, is destructive in colour and in words, and focuses the safe choice, Keep agent open.</summary>
    [Fact]
    public async Task TheCloseBarFocusesTheSafeChoice()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var bar = Open(out var window, out var model, out _, out var dismissed);
            var confirmed = 0;
            bar.CloseConfirmed += (_, _) => confirmed++;

            bar.OpenClose();
            ViewHarness.Settle(window);

            bar.IsCloseOpen.ShouldBeTrue();
            bar.IsStopOpen.ShouldBeFalse();
            var region = ViewHarness.Find<Border>(bar, "CloseConfirmBar");
            AutomationProperties.GetName(region).ShouldBe("Confirm close");
            AutomationProperties.GetLiveSetting(region).ShouldBe(AutomationLiveSetting.Polite);
            ViewHarness.VisibleTexts(bar).ShouldContain(AgentPanelCopy.CloseConfirmation("Build the rail"), StringComparer.Ordinal);
            ViewHarness.TabOrder(bar).ShouldBe(["Close agent", "Keep agent open"]);
            ViewHarness.Find<Button>(bar, "CloseAgentButton").Classes.Contains("destructive").ShouldBeTrue();
            ViewHarness.Find<Button>(bar, "KeepAgentOpenButton").IsFocused.ShouldBeTrue("the safe default is focused");

            ViewHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            bar.IsCloseOpen.ShouldBeFalse();
            dismissed.ShouldBe(new[] { ConfirmBarKind.Close });
            confirmed.ShouldBe(0, "Enter on the safe default keeps the agent open");

            bar.OpenClose();
            ViewHarness.Settle(window);
            ViewHarness.Click(window, ViewHarness.Find<Button>(bar, "CloseAgentButton"));
            confirmed.ShouldBe(1);
            bar.IsCloseOpen.ShouldBeFalse();
            model.Dispose();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Esc dismisses the close bar and opening one bar closes the other.</summary>
    [Fact]
    public async Task EscDismissesTheCloseBarAndOneBarReplacesTheOther()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var bar = Open(out var window, out var model, out _, out var dismissed);

            bar.OpenClose();
            ViewHarness.Settle(window);
            ViewHarness.Press(window, Key.Escape, PhysicalKey.Escape);
            bar.IsCloseOpen.ShouldBeFalse();
            dismissed.ShouldBe(new[] { ConfirmBarKind.Close });

            bar.OpenStop();
            bar.OpenClose();
            bar.IsStopOpen.ShouldBeFalse();
            bar.IsCloseOpen.ShouldBeTrue();
            bar.Dismiss();
            bar.IsCloseOpen.ShouldBeFalse();
            ViewHarness.Unnamed(bar).ShouldBeEmpty();
            model.Dispose();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Switching to another session closes an open bar without an event, and the new session's title reaches the close text.</summary>
    [Fact]
    public async Task ADataContextSwapClosesTheBarsSilently()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var bar = Open(out var window, out var first, out _, out var dismissed);
            var clock = new ManualTimeProvider();
            using var second = PanelFixtures.Session(PanelFixtures.Row("s2", SessionLifecycle.Running, clock, title: "Second agent"), clock);

            bar.OpenClose();
            bar.DataContext = second;
            ViewHarness.Settle(window);

            bar.IsCloseOpen.ShouldBeFalse();
            dismissed.ShouldBeEmpty("the bar belonged to the session that was shown");

            bar.OpenClose();
            ViewHarness.Settle(window);
            ViewHarness.VisibleTexts(bar).ShouldContain(AgentPanelCopy.CloseConfirmation("Second agent"), StringComparer.Ordinal);

            bar.DataContext = null;
            ViewHarness.Settle(window);
            bar.IsCloseOpen.ShouldBeFalse();
            first.Dispose();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
