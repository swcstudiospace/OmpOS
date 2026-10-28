using Avalonia.Automation;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Runs;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.RunControls;

/// <summary>
/// Steering and follow-ups from the composer while a run is active (ADE-03). A steer reaches the run at once; a follow-up waits
/// in send order until the active run completes. The session's engine holds an accepted steer and sends it as the next turn, because
/// the engine has no steer command.
/// </summary>
public sealed class ComposerRunInputTests
{
    private static readonly string[] BeginOnly = ["begin"];
    private static readonly string[] BeginAndSteer = ["begin", "change course"];
    private static readonly string[] BeginAndFirst = ["begin", "first"];
    private static readonly string[] BeginFirstAndSecond = ["begin", "first", "second"];

    /// <summary>While a run is active, the send control steers the run at once and clears the draft the run accepted.</summary>
    [Fact]
    public async Task WhileARunIsActiveSendSteersTheRunAtOnceAndClearsTheDraft()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var channel = new GatedPromptChannel();
            var window = RunControlsHarness.Show(new ScriptedApprovalBridge(), channel);
            var runtime = ShellHarness.RuntimeOf(window);
            await StartRunAsync(window, channel, "begin").ConfigureAwait(true);

            ShellHarness.Prompt(window).Text = "change course";
            AutomationProperties.GetName(ShellHarness.Send(window)).ShouldBe("Steer message");

            RunControlsHarness.Click(ShellHarness.Send(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            runtime.Runs.SteersHeld.ShouldBe(1, "the run's input holds the steer it accepted");
            runtime.ViewModel.Steering.History.ShouldHaveSingleItem().Text.ShouldBe("change course");
            channel.Prompts.ShouldBe(BeginOnly, "the steer does not cancel or replace the turn in flight");
            runtime.Runs.IsRunActive.ShouldBeTrue("steering does not end the run");
            runtime.Composer.Draft.ShouldBe(string.Empty);
            ShellHarness.Prompt(window).Text.ShouldBe(string.Empty);

            // The held steer is delivered as the next turn of the run once the turn in flight ends.
            channel.ReleaseTurn();
            await RunControlsHarness.UntilAsync(() => channel.Prompts.Count == 2, "the steer's turn", TestContext.Current.CancellationToken).ConfigureAwait(true);

            channel.Prompts.ShouldBe(BeginAndSteer);
            runtime.Runs.SteersHeld.ShouldBe(0);
            runtime.Runs.IsRunActive.ShouldBeTrue();

            channel.ReleaseTurn();
            await RunControlsHarness.UntilAsync(() => !runtime.Runs.IsRunActive, "the run to end", TestContext.Current.CancellationToken).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Follow-ups queue in send order and dispatch one at a time, each only after the run before it completes.</summary>
    [Fact]
    public async Task FollowUpsAreDispatchedOnlyAfterCompletionInSendOrder()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var channel = new GatedPromptChannel();
            var window = RunControlsHarness.Show(new ScriptedApprovalBridge(), channel);
            var runtime = ShellHarness.RuntimeOf(window);
            await StartRunAsync(window, channel, "begin").ConfigureAwait(true);

            ShellHarness.Prompt(window).Text = "first";
            RunControlsHarness.Click(RunControlsHarness.FollowUp(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Prompt(window).Text = "second";
            RunControlsHarness.Click(RunControlsHarness.FollowUp(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            runtime.Runs.QueuedFollowUpCount.ShouldBe(2);
            channel.Prompts.ShouldBe(BeginOnly, "no follow-up starts while the run is active");
            RunControlsHarness.PendingLabel(window).IsVisible.ShouldBeTrue();
            RunControlsHarness.PendingText(window).ShouldContain("2 follow-ups");

            channel.ReleaseTurn();
            await RunControlsHarness.UntilAsync(() => channel.Prompts.Count == 2, "the first follow-up's turn", TestContext.Current.CancellationToken).ConfigureAwait(true);

            channel.Prompts.ShouldBe(BeginAndFirst);
            runtime.Runs.QueuedFollowUpCount.ShouldBe(1);
            runtime.Runs.IsRunActive.ShouldBeTrue("the dispatched follow-up starts a run");

            channel.ReleaseTurn();
            await RunControlsHarness.UntilAsync(() => channel.Prompts.Count == 3, "the second follow-up's turn", TestContext.Current.CancellationToken).ConfigureAwait(true);

            channel.Prompts.ShouldBe(BeginFirstAndSecond);
            runtime.Runs.QueuedFollowUpCount.ShouldBe(0);
            runtime.Runs.IsRunActive.ShouldBeTrue("the second follow-up starts a run");

            channel.ReleaseTurn();
            await RunControlsHarness.UntilAsync(() => !runtime.Runs.IsRunActive, "the run to end", TestContext.Current.CancellationToken).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// A steer the run refuses keeps the draft and reports the refusal on the status line. The engine's run input never faults a steer, so
    /// the refusal that remains is a full queue: the run already holds the most messages it accepts.
    /// </summary>
    [Fact]
    public async Task ASteerTheRunRefusesKeepsTheDraftAndReportsTheRefusal()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var channel = new GatedPromptChannel();
            var window = RunControlsHarness.Show(new ScriptedApprovalBridge(), channel);
            var runtime = ShellHarness.RuntimeOf(window);
            await StartRunAsync(window, channel, "begin").ConfigureAwait(true);

            for (var held = 0; held < RunInputScheduler.MaxQueuedMessages; held++)
            {
                ShellHarness.Prompt(window).Text = $"steer {held}";
                RunControlsHarness.Click(ShellHarness.Send(window));
                await ShellHarness.DrainAsync(window).ConfigureAwait(true);
            }

            runtime.Runs.SteersHeld.ShouldBe(RunInputScheduler.MaxQueuedMessages);

            ShellHarness.Prompt(window).Text = "keep me";
            RunControlsHarness.Click(ShellHarness.Send(window));

            var expected = AgentPanelCopy.QueueFull(RunInputScheduler.MaxQueuedMessages);
            await RunControlsHarness.UntilAsync(() => string.Equals(ShellHarness.Status(window).Text, expected, StringComparison.Ordinal), "the refusal", TestContext.Current.CancellationToken).ConfigureAwait(true);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Status(window).Text.ShouldBe(expected);
            runtime.Composer.Draft.ShouldBe("keep me");
            runtime.Runs.SteersHeld.ShouldBe(RunInputScheduler.MaxQueuedMessages, "the run accepted no further steer");
            runtime.ViewModel.Steering.History.ShouldNotContain(entry => string.Equals(entry.Text, "keep me", StringComparison.Ordinal));

            await StopRunAsync(window, runtime).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With no engine connected, a send is refused visibly and keeps the draft.</summary>
    [Fact]
    public async Task AnUnconnectedSessionRefusesTheSendVisiblyAndKeepsTheDraft()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = RunControlsHarness.ShowDefaults();
            var runtime = ShellHarness.RuntimeOf(window);

            ShellHarness.Prompt(window).Text = "keep me";
            RunControlsHarness.Click(ShellHarness.Send(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Status(window).Text.ShouldBe(MainView.RefusedSendStatus);
            runtime.Composer.Draft.ShouldBe("keep me");
            runtime.Runs.IsRunActive.ShouldBeFalse("a refused send starts no run");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>When the run input does not accept the dispatch of a follow-up, the follow-up stays queued: nothing is lost when the run completes.</summary>
    [Fact]
    public async Task AFollowUpTheRunInputDoesNotAcceptStaysQueuedWhenTheRunCompletes()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = RunControlsHarness.ShowDefaults();
            var runtime = ShellHarness.RuntimeOf(window);
            runtime.Runs.RunStarted();

            ShellHarness.Prompt(window).Text = "later";
            RunControlsHarness.Click(RunControlsHarness.FollowUp(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            // A delivery that is cancelled is one the run input refuses to take.
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync().ConfigureAwait(true);

            await Should.ThrowAsync<OperationCanceledException>(() => runtime.Runs.RunCompletedAsync(cancelled.Token)).ConfigureAwait(true);
            runtime.Runs.QueuedFollowUpCount.ShouldBe(1);
            runtime.Runs.IsRunActive.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The steer and follow-up controls are offered only while a run is active, and the label states the pending input.</summary>
    [Fact]
    public async Task SteerAndFollowUpAreOfferedOnlyWhileARunIsActive()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = RunControlsHarness.Show(new ScriptedApprovalBridge());
            var runtime = ShellHarness.RuntimeOf(window);

            RunControlsHarness.FollowUp(window).IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(ShellHarness.Send(window)).ShouldBe("Send message");
            RunControlsHarness.PendingLabel(window).IsVisible.ShouldBeFalse();

            runtime.Runs.RunStarted();
            ShellHarness.Settle(window);

            RunControlsHarness.FollowUp(window).IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(ShellHarness.Send(window)).ShouldBe("Steer message");
            RunControlsHarness.PendingLabel(window).IsVisible.ShouldBeTrue();
            RunControlsHarness.PendingText(window).ShouldContain("A run is active");

            await runtime.Runs.RunCompletedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            ShellHarness.Settle(window);

            RunControlsHarness.FollowUp(window).IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(ShellHarness.Send(window)).ShouldBe("Send message");
            RunControlsHarness.PendingLabel(window).IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Sends the first prompt of a run through the composer and waits until the channel's turn has started.</summary>
    /// <param name="window">The shown window.</param>
    /// <param name="channel">The send path of the window's session.</param>
    /// <param name="prompt">The prompt that starts the run.</param>
    /// <returns>A task that completes when the run's first turn is in flight.</returns>
    private static async Task StartRunAsync(MainWindow window, GatedPromptChannel channel, string prompt)
    {
        ShellHarness.Prompt(window).Text = prompt;
        RunControlsHarness.Click(ShellHarness.Send(window));
        await ShellHarness.DrainAsync(window).ConfigureAwait(true);
        await RunControlsHarness.UntilAsync(() => channel.Prompts.Count == 1, "the first turn", TestContext.Current.CancellationToken).ConfigureAwait(true);
        ShellHarness.Settle(window);
    }

    /// <summary>Stops the run with the Stop button and waits until the run has ended.</summary>
    /// <param name="window">The shown window.</param>
    /// <param name="runtime">The runtime of the window's session.</param>
    /// <returns>A task that completes when the run has ended.</returns>
    private static async Task StopRunAsync(MainWindow window, AgentSessionRuntime runtime)
    {
        RunControlsHarness.Click(ShellHarness.Stop(window));
        await ShellHarness.DrainAsync(window).ConfigureAwait(true);
        await RunControlsHarness.UntilAsync(() => !runtime.Runs.IsRunActive, "the run to stop", TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
