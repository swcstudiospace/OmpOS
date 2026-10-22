using Avalonia.Automation;
using OmpOS.Native.Engine;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.RunControls;

/// <summary>
/// Steering and follow-ups from the composer while a run is active (ADE-03). A steer reaches the run at once; a follow-up waits
/// in send order until the active run completes.
/// </summary>
public sealed class ComposerRunInputTests
{
    private static readonly string[] FirstOnly = ["first"];
    private static readonly string[] FirstAndSecond = ["first", "second"];

    /// <summary>While a run is active, the send control steers the run at once and clears the draft the run accepted.</summary>
    [Fact]
    public async Task WhileARunIsActiveSendSteersTheRunAtOnceAndClearsTheDraft()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var input = new RecordingRunInput();
            var window = RunControlsHarness.Show(new ScriptedApprovalBridge(), input);
            window.Runs.RunStarted();

            ShellHarness.Prompt(window).Text = "change course";
            AutomationProperties.GetName(ShellHarness.Send(window)).ShouldBe("Steer message");

            RunControlsHarness.Click(ShellHarness.Send(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            input.Steers.ShouldHaveSingleItem().ShouldBe("change course");
            window.Runs.IsRunActive.ShouldBeTrue("steering does not end the run");
            window.Composer.Draft.ShouldBe(string.Empty);
            ShellHarness.Prompt(window).Text.ShouldBe(string.Empty);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Follow-ups queue in send order and dispatch one at a time, each only after the run before it completes.</summary>
    [Fact]
    public async Task FollowUpsAreDispatchedOnlyAfterCompletionInSendOrder()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var input = new RecordingRunInput();
            var window = RunControlsHarness.Show(new ScriptedApprovalBridge(), input);
            window.Runs.RunStarted();

            ShellHarness.Prompt(window).Text = "first";
            RunControlsHarness.Click(RunControlsHarness.FollowUp(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Prompt(window).Text = "second";
            RunControlsHarness.Click(RunControlsHarness.FollowUp(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            window.Runs.QueuedFollowUpCount.ShouldBe(2);
            input.FollowUps.ShouldBeEmpty("no follow-up starts while the run is active");
            RunControlsHarness.PendingLabel(window).IsVisible.ShouldBeTrue();
            RunControlsHarness.PendingText(window).ShouldContain("2 follow-ups");

            await window.Runs.RunCompletedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            input.FollowUps.ShouldBe(FirstOnly);
            window.Runs.QueuedFollowUpCount.ShouldBe(1);
            window.Runs.IsRunActive.ShouldBeTrue("the dispatched follow-up starts a run");

            await window.Runs.RunCompletedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            input.FollowUps.ShouldBe(FirstAndSecond);
            window.Runs.QueuedFollowUpCount.ShouldBe(0);
            window.Runs.IsRunActive.ShouldBeTrue("the second follow-up starts a run");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A steer the run rejects keeps the draft and reports the fault on the status line.</summary>
    [Fact]
    public async Task ASteerTheRunRejectsKeepsTheDraftAndReportsTheFault()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var input = new RecordingRunInput { SteerFault = new InvalidOperationException("The run rejected the steer.") };
            var window = RunControlsHarness.Show(new ScriptedApprovalBridge(), input);
            window.Runs.RunStarted();

            ShellHarness.Prompt(window).Text = "keep me";
            RunControlsHarness.Click(ShellHarness.Send(window));

            await RunControlsHarness.UntilAsync(() => ShellHarness.Status(window).Text?.Contains("rejected", StringComparison.Ordinal) == true, "the steer fault", TestContext.Current.CancellationToken).ConfigureAwait(true);
            ShellHarness.Status(window).Text.ShouldBe($"{MainWindow.FaultedStatus} The run rejected the steer.");
            window.Composer.Draft.ShouldBe("keep me");
            input.Steers.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With no run input connected, a steer fails visibly and keeps the draft.</summary>
    [Fact]
    public async Task TheDefaultRunInputFailsASteerVisiblyAndKeepsTheDraft()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = RunControlsHarness.ShowDefaults();
            window.Runs.RunStarted();

            ShellHarness.Prompt(window).Text = "keep me";
            RunControlsHarness.Click(ShellHarness.Send(window));

            await RunControlsHarness.UntilAsync(() => ShellHarness.Status(window).Text?.Contains(UnconnectedRunInput.UnavailableText, StringComparison.Ordinal) == true, "the default fault", TestContext.Current.CancellationToken).ConfigureAwait(true);
            window.Composer.Draft.ShouldBe("keep me");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With no run input connected, a follow-up stays queued: nothing is lost when the run completes.</summary>
    [Fact]
    public async Task TheDefaultRunInputKeepsAFollowUpQueuedWhenTheRunCompletes()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = RunControlsHarness.ShowDefaults();
            window.Runs.RunStarted();

            ShellHarness.Prompt(window).Text = "later";
            RunControlsHarness.Click(RunControlsHarness.FollowUp(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            await Should.ThrowAsync<InvalidOperationException>(() => window.Runs.RunCompletedAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
            window.Runs.QueuedFollowUpCount.ShouldBe(1);
            window.Runs.IsRunActive.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The steer and follow-up controls are offered only while a run is active, and the label states the pending input.</summary>
    [Fact]
    public async Task SteerAndFollowUpAreOfferedOnlyWhileARunIsActive()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = RunControlsHarness.Show(new ScriptedApprovalBridge(), new RecordingRunInput());

            RunControlsHarness.FollowUp(window).IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(ShellHarness.Send(window)).ShouldBe("Send message");
            RunControlsHarness.PendingLabel(window).IsVisible.ShouldBeFalse();

            window.Runs.RunStarted();
            ShellHarness.Settle(window);

            RunControlsHarness.FollowUp(window).IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(ShellHarness.Send(window)).ShouldBe("Steer message");
            RunControlsHarness.PendingLabel(window).IsVisible.ShouldBeTrue();
            RunControlsHarness.PendingText(window).ShouldContain("A run is active");

            await window.Runs.RunCompletedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            ShellHarness.Settle(window);

            RunControlsHarness.FollowUp(window).IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(ShellHarness.Send(window)).ShouldBe("Send message");
            RunControlsHarness.PendingLabel(window).IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
