using Autonogrammer.Native.Views;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.Runs;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Views;

/// <summary>The composer's panel behaviour: Send or Steer, Follow-up, Stop, the hint line, the key map and the refused-message text (46-UI-SPEC, Composer Behaviour).</summary>
public sealed class ComposerViewPanelTests
{
    private static readonly string[] SizedButtons = ["SendButton", "StopButton"];

    private static readonly string[] TransitionButtons = ["SendButton", "FollowUpButton", "StopButton"];

    private sealed record Rig(ComposerView View, ComposerViewModel Composer, ScriptedRunInput Input, RunInputScheduler Scheduler, List<string> Sent, StopCounter Stops, Window Window);

    private sealed class StopCounter
    {
        internal int Count { get; set; }
    }

    private static Rig Open(bool runActive, bool streaming = false, bool reducedMotion = false, bool withSteering = false, bool live = false)
    {
        var sent = new List<string>();
        var stops = new StopCounter();
        var composer = new ComposerViewModel(
            text =>
            {
                sent.Add(text);
                return Task.FromResult(true);
            },
            () =>
            {
                stops.Count++;
                return Task.CompletedTask;
            })
        {
            IsStreaming = streaming,
        };
        var input = new ScriptedRunInput();
        var scheduler = new RunInputScheduler(input);
        if (runActive)
        {
            scheduler.RunStarted();
        }

        var view = new ComposerView { DataContext = composer, Runs = scheduler };
        if (withSteering)
        {
            view.Steering = live
                ? new SteeringInputModel(
                    scheduler,
                    new ManualTimeProvider(),
                    static (_, _) => Task.CompletedTask,
                    static (_, _, _) => Task.FromResult(new SteeringDelivery(SteeringDeliveryStatus.Delivered)))
                : new SteeringInputModel(scheduler, new ManualTimeProvider(), static (_, _) => Task.CompletedTask);
        }

        var window = ViewHarness.Show(view, reducedMotion, width: 800, height: 400);
        return new Rig(view, composer, input, scheduler, sent, stops, window);
    }

    private static async Task DrainAsync(Rig rig)
    {
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnSettled(object? sender, EventArgs e) => settled.TrySetResult();

        rig.View.WorkSettled += OnSettled;
        try
        {
            if (rig.View.IsWorking)
            {
                await settled.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            rig.View.WorkSettled -= OnSettled;
        }

        ViewHarness.Settle(rig.Window);
    }

    private static TextBox Prompt(Rig rig) => ViewHarness.Find<TextBox>(rig.View, "PromptInput");

    private static async Task PressAsync(Rig rig, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Prompt(rig).Focus();
        ViewHarness.Press(rig.Window, key, physical, modifiers);
        await DrainAsync(rig).ConfigureAwait(true);
    }

    /// <summary>
    /// While idle the composer offers Send, the idle placeholder and no follow-up. The hint is the tooltip and help text of the prompt, not a
    /// visible line (cursor-agents-window-parity.md 4.7: HintLine is IsVisible=false).
    /// </summary>
    [Fact]
    public async Task IdleShowsSendAndTheHint()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var rig = Open(runActive: false);
            var send = ViewHarness.Find<Button>(rig.View, "SendButton");
            var hint = ViewHarness.Find<TextBlock>(rig.View, "HintLine");

            Prompt(rig).PlaceholderText.ShouldBe(AgentPanelCopy.ComposerPlaceholderIdle);
            AutomationProperties.GetName(send).ShouldBe("Send message");
            ViewHarness.Find<Button>(rig.View, "FollowUpButton").IsVisible.ShouldBeFalse();
            hint.Text.ShouldBe("Enter sends. Shift+Enter adds a line. While a run is active: Ctrl+Enter steers, Ctrl+Shift+Enter queues a follow-up, Esc stops.");
            hint.IsEffectivelyVisible.ShouldBeFalse("the hint is a tooltip since the composer became a card (spec 4.7)");
            ToolTip.GetTip(Prompt(rig)).ShouldBe(hint.Text);
            AutomationProperties.GetName(hint).ShouldBe("Composer hint");
            AutomationProperties.GetHelpText(Prompt(rig)).ShouldBe(hint.Text);
            hint.Focusable.ShouldBeFalse("the hint is text, not a tab stop");
            AutomationProperties.GetLiveSetting(hint).ShouldBe(AutomationLiveSetting.Off);
            rig.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With live steering the running composer says a steer is sent now and a follow-up is queued, never that it is delivered after the turn.</summary>
    [Fact]
    public async Task LiveSteeringShowsSendNowAndQueueWording()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var rig = Open(runActive: true, streaming: true, withSteering: true, live: true);
            var send = ViewHarness.Find<Button>(rig.View, "SendButton");
            var followUp = ViewHarness.Find<Button>(rig.View, "FollowUpButton");
            var pending = ViewHarness.Find<TextBlock>(rig.View, "PendingLabel");

            Prompt(rig).PlaceholderText.ShouldBe(AgentPanelCopy.ComposerPlaceholderRunningLive);
            Prompt(rig).PlaceholderText.ShouldNotBeNull().ShouldNotContain("after the active turn", Case.Sensitive);
            AutomationProperties.GetName(send).ShouldBe("Send now, deliver the message into the running turn");
            ToolTip.GetTip(send).ShouldBe("Send now");
            followUp.Content.ShouldBe("Queue follow-up");
            AutomationProperties.GetName(followUp).ShouldBe(AgentPanelCopy.QueueFollowUpName);
            pending.Text.ShouldBe(AgentPanelCopy.ComposerPendingActiveLive);
            ViewHarness.TabOrder(rig.View).ShouldBe(["Message", "Send now, deliver the message into the running turn", AgentPanelCopy.QueueFollowUpName, "Stop response"]);

            await rig.Scheduler.RunCompletedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            ViewHarness.Settle(rig.Window);
            Prompt(rig).PlaceholderText.ShouldBe(AgentPanelCopy.ComposerPlaceholderIdle);
            AutomationProperties.GetName(send).ShouldBe("Send message");
            rig.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Without live steering the old wording stays: a steer is delivered after the active turn, even with a steering model.</summary>
    [Fact]
    public async Task NonLiveSteeringKeepsTheAfterTheTurnWording()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var rig = Open(runActive: true, streaming: true, withSteering: true, live: false);

            Prompt(rig).PlaceholderText.ShouldBe(AgentPanelCopy.ComposerPlaceholderRunning);
            AutomationProperties.GetName(ViewHarness.Find<Button>(rig.View, "SendButton")).ShouldBe("Steer message");
            AutomationProperties.GetName(ViewHarness.Find<Button>(rig.View, "FollowUpButton")).ShouldBe("Queue follow-up message");
            rig.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>While a run is active the composer steers: Steer name, the running placeholder and a visible Queue follow-up button.</summary>
    [Fact]
    public async Task StreamingShowsStopAndSteer()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var rig = Open(runActive: true, streaming: true);
            var followUp = ViewHarness.Find<Button>(rig.View, "FollowUpButton");

            Prompt(rig).PlaceholderText.ShouldBe(AgentPanelCopy.ComposerPlaceholderRunning);
            AutomationProperties.GetName(ViewHarness.Find<Button>(rig.View, "SendButton")).ShouldBe("Steer message");
            followUp.IsEffectivelyVisible.ShouldBeTrue();
            followUp.Content.ShouldBe("Queue follow-up");
            AutomationProperties.GetName(followUp).ShouldBe("Queue follow-up message");
            followUp.MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            ViewHarness.TabOrder(rig.View).ShouldBe(["Message", "Steer message", "Queue follow-up message", "Stop response"]);
            ViewHarness.Unnamed(rig.View).ShouldBeEmpty();

            await rig.Scheduler.RunCompletedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            ViewHarness.Settle(rig.Window);
            followUp.IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(ViewHarness.Find<Button>(rig.View, "SendButton")).ShouldBe("Send message");
            rig.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Send and Stop are always enabled and always in the tab order, with targets of at least the WCAG 2.2 minimum (24 px).</summary>
    [Fact]
    public async Task SendAndStopStayEnabledAndLargeEnough()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var rig = Open(runActive: false);

            foreach (var name in SizedButtons)
            {
                var button = ViewHarness.Find<Button>(rig.View, name);
                button.IsEnabled.ShouldBeTrue();
                button.Width.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
                button.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            }

            ViewHarness.TabOrder(rig.View).ShouldBe(["Message", "Send message", "Stop response"]);
            rig.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter and Ctrl+Enter send while idle; Shift+Enter and Ctrl+Shift+Enter do not.</summary>
    [Fact]
    public async Task IdleKeyMap()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var rig = Open(runActive: false);
            Prompt(rig).Text = "first";
            Prompt(rig).CaretIndex = "first".Length;

            await PressAsync(rig, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Shift).ConfigureAwait(true);
            rig.Sent.ShouldBeEmpty("Shift+Enter adds a line");

            await PressAsync(rig, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control | RawInputModifiers.Shift).ConfigureAwait(true);
            rig.Sent.ShouldBeEmpty("Ctrl+Shift+Enter does nothing while idle");
            rig.Composer.Draft.ShouldStartWith("first");

            Prompt(rig).Text = "second";
            await PressAsync(rig, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control).ConfigureAwait(true);
            rig.Sent.ShouldBe(["second"]);

            Prompt(rig).Text = "third";
            await PressAsync(rig, Key.Enter, PhysicalKey.Enter).ConfigureAwait(true);
            rig.Sent.ShouldBe(["second", "third"]);
            rig.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter and Ctrl+Enter steer the active run through the steering model, which records the entry; the draft clears.</summary>
    [Fact]
    public async Task EnterAndCtrlEnterSteerWhileARunIsActive()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var rig = Open(runActive: true, streaming: true, withSteering: true);
            Prompt(rig).Text = "go left";

            await PressAsync(rig, Key.Enter, PhysicalKey.Enter).ConfigureAwait(true);
            Prompt(rig).Text = "go right";
            await PressAsync(rig, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control).ConfigureAwait(true);

            rig.Input.Steers.ShouldBe(["go left", "go right"]);
            rig.Sent.ShouldBeEmpty("a steer is not a new prompt");
            rig.View.Steering!.History.Count.ShouldBe(2);
            rig.View.Steering.History.All(static entry => entry.Mode == SteeringMode.Steer).ShouldBeTrue();
            rig.Composer.Draft.ShouldBeEmpty();
            rig.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Ctrl+Shift+Enter and the Follow-up button queue a follow-up, which appears in the history and keeps its order.</summary>
    [Fact]
    public async Task CtrlShiftEnterAndTheButtonQueueFollowUps()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var rig = Open(runActive: true, streaming: true, withSteering: true);
            Prompt(rig).Text = "then run the tests";

            await PressAsync(rig, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control | RawInputModifiers.Shift).ConfigureAwait(true);
            rig.Scheduler.QueuedFollowUpCount.ShouldBe(1);
            rig.View.Steering!.History.Single().Mode.ShouldBe(SteeringMode.FollowUp);

            Prompt(rig).Text = "and the docs";
            ViewHarness.Click(rig.Window, ViewHarness.Find<Button>(rig.View, "FollowUpButton"));
            await DrainAsync(rig).ConfigureAwait(true);

            rig.Scheduler.QueuedFollowUpCount.ShouldBe(2);
            rig.View.Steering.History.Count.ShouldBe(2);
            ViewHarness.Find<TextBlock>(rig.View, "PendingLabel").Text.ShouldNotBeNull().ShouldContain("2 follow-ups are queued");
            rig.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Without a steering model the composer still steers and queues through the scheduler, as before.</summary>
    [Fact]
    public async Task WithoutASteeringModelTheSchedulerIsUsedDirectly()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var rig = Open(runActive: true, streaming: true);
            Prompt(rig).Text = "steer me";
            await PressAsync(rig, Key.Enter, PhysicalKey.Enter).ConfigureAwait(true);
            Prompt(rig).Text = "queue me";
            await PressAsync(rig, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control | RawInputModifiers.Shift).ConfigureAwait(true);

            rig.Input.Steers.ShouldBe(["steer me"]);
            rig.Scheduler.QueuedFollowUpCount.ShouldBe(1);
            rig.Composer.Draft.ShouldBeEmpty();
            rig.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A full queue refuses the message with the queue-full text on the status event and keeps the draft.</summary>
    [Fact]
    public async Task AFullQueueRefusesAndKeepsTheDraft()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var rig = Open(runActive: true, streaming: true, withSteering: true);
            for (var index = 0; index < RunInputScheduler.MaxQueuedMessages; index++)
            {
                (await rig.View.Steering!.SubmitAsync($"follow-up {index}", SteeringMode.FollowUp, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Accepted);
            }

            var messages = new List<string>();
            rig.View.StatusMessage += (_, e) => messages.Add(e.Message);
            Prompt(rig).Text = "one too many";

            await PressAsync(rig, Key.Enter, PhysicalKey.Enter).ConfigureAwait(true);

            messages.ShouldBe(new[] { AgentPanelCopy.QueueFull(RunInputScheduler.MaxQueuedMessages) });
            rig.Composer.Draft.ShouldBe("one too many");
            rig.View.Steering!.History.Count.ShouldBe(RunInputScheduler.MaxQueuedMessages);
            rig.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Held steering messages and queued follow-ups are counted in the polite pending line.</summary>
    [Fact]
    public async Task HeldAndQueuedCountsAreShown()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var rig = Open(runActive: true, streaming: true);
            var label = ViewHarness.Find<TextBlock>(rig.View, "PendingLabel");

            AutomationProperties.GetLiveSetting(label).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetName(label).ShouldBe("Pending input");

            rig.Input.SteersHeld = 2;
            rig.Scheduler.NotifySteersHeldChanged();
            rig.Scheduler.QueueFollowUp("later");
            ViewHarness.Settle(rig.Window);

            label.IsEffectivelyVisible.ShouldBeTrue();
            label.Text.ShouldNotBeNull().ShouldContain(AgentPanelCopy.HeldSteering(2)!);
            label.Text.ShouldContain("1 follow-up is queued");
            rig.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Esc while a run is active asks the owner for the stop confirmation instead of stopping; without an owner it stops at once.</summary>
    [Fact]
    public async Task EscAsksForConfirmationWhenTheShellListens()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var asking = Open(runActive: true, streaming: true);
            var requests = 0;
            asking.View.StopConfirmationRequested += (_, _) => requests++;
            await PressAsync(asking, Key.Escape, PhysicalKey.Escape).ConfigureAwait(true);

            requests.ShouldBe(1);
            asking.Stops.Count.ShouldBe(0, "the confirmation bar decides");
            asking.Window.Close();

            var plain = Open(runActive: true, streaming: true);
            await PressAsync(plain, Key.Escape, PhysicalKey.Escape).ConfigureAwait(true);

            plain.Stops.Count.ShouldBe(1, "nothing listens, so Esc stops as it always did");
            plain.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Esc with nothing running does nothing, and the Stop button stops at once without a confirmation.</summary>
    [Fact]
    public async Task EscIdleIsANoOpAndTheStopButtonStopsAtOnce()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var rig = Open(runActive: false);
            var requests = 0;
            rig.View.StopConfirmationRequested += (_, _) => requests++;

            await PressAsync(rig, Key.Escape, PhysicalKey.Escape).ConfigureAwait(true);
            requests.ShouldBe(0);
            rig.Stops.Count.ShouldBe(0);

            ViewHarness.Click(rig.Window, ViewHarness.Find<Button>(rig.View, "StopButton"));
            await DrainAsync(rig).ConfigureAwait(true);
            rig.Stops.Count.ShouldBe(1);
            rig.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The hint line is hidden by default (the hint is a tooltip, spec 4.7), can be shown by the shell, and focus moves to the prompt.</summary>
    [Fact]
    public async Task TheHintCanBeHiddenAndFocusMovesToThePrompt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var rig = Open(runActive: false);

            rig.View.HintVisible.ShouldBeFalse("the hint is a tooltip by default (spec 4.7)");
            rig.View.HintVisible = true;
            ViewHarness.Settle(rig.Window);
            ViewHarness.Find<TextBlock>(rig.View, "HintLine").IsEffectivelyVisible.ShouldBeTrue();
            rig.View.HintVisible = false;
            ViewHarness.Settle(rig.Window);
            ViewHarness.Find<TextBlock>(rig.View, "HintLine").IsEffectivelyVisible.ShouldBeFalse();

            ViewHarness.Find<Button>(rig.View, "StopButton").Focus();
            rig.View.FocusPrompt();
            Prompt(rig).IsFocused.ShouldBeTrue();
            rig.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Under reduced motion the composer buttons keep no transition; under full motion the Button background transitions.</summary>
    [Fact]
    public async Task ReducedMotionRemovesTheButtonTransitions()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var reduced = Open(runActive: true, reducedMotion: true);
            foreach (var name in TransitionButtons)
            {
                (ViewHarness.Find<Button>(reduced.View, name).Transitions?.Count ?? 0).ShouldBe(0, $"{name} keeps no transition under reduced motion");
            }

            reduced.Window.Close();

            var full = Open(runActive: true);
            ViewHarness.Find<Button>(full.View, "SendButton").Transitions!.OfType<BrushTransition>().ShouldHaveSingleItem();
            full.Window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
