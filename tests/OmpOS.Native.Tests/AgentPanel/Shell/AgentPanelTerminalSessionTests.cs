using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Steering;
using OmpOS.Native.Tests.AgentPanel.Views;
using OmpOS.Native.Tests.Terminal;
using OmpOS.Native.Views.Sessions;
using OmpOS.Native.Views.Terminal;
using OmpOS.Presentation;
using OmpOS.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>
/// An omp terminal session in the shell: New omp terminal creates it, the thread shows the terminal instead of a transcript, the composer is
/// steer-only, switching sessions and tabs never restarts omp, and closing the session ends it.
/// </summary>
public sealed class AgentPanelTerminalSessionTests
{
    private static readonly string[] SteerButtons = ["SendNowButton", "QueueButton"];
    private static readonly MotionPreference[] Motions = [MotionPreference.Reduced, MotionPreference.Full];
    private static readonly string[] FontTokens = ["OmpOS.Font.Family", "OmpOS.Font.Display", "OmpOS.Font.Mono"];

    /// <summary>Ctrl+Shift+T creates a terminal session, selects it, shows the terminal in the thread and gives it focus, and omp starts once.</summary>
    [Fact]
    public async Task CtrlShiftTCreatesAndShowsATerminalSession()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);

            ShellHarness.Press(window, Key.T, PhysicalKey.T, shift: true, control: true);
            await ShellWait.UntilAsync(window, () => string.Equals(fixture.Panel.Selected?.Id, "terminal-1", StringComparison.Ordinal), "the terminal session", token).ConfigureAwait(true);
            var (runtime, starter, _) = Parts(fixture, "terminal-1");
            await ShellWait.UntilAsync(window, () => runtime.Terminal!.State == OmpTerminalState.Running, "omp to start", token).ConfigureAwait(true);

            fixture.Host.CreatedTerminals.ShouldBe(["terminal-1"]);
            runtime.Kind.ShouldBe(SessionKind.Terminal);
            var thread = main.FindControl<ContentControl>("TranscriptHost")!.Content.ShouldBeOfType<OmpTerminalView>();
            thread.DataContext.ShouldBeSameAs(runtime.Terminal);
            main.FindControl<ContentControl>("ComposerHost")!.Content.ShouldBeOfType<TerminalSteerView>();
            starter.Sizes.Count.ShouldBe(1, "omp starts once, when the terminal is first shown");
            var output = thread.FindControl<Control>("Output")!;
            await ShellWait.UntilAsync(window, () => BoardShell.FocusIn(window, output), "the terminal to take focus (its inner view holds the keyboard focus)", token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The rail offers New omp terminal beside New agent, with a name and a tooltip, and a terminal row carries the Terminal tag.</summary>
    [Fact]
    public async Task TheRailOffersNewOmpTerminalAndTagsTerminalRows()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            var button = ShellTree.Named<Button>(main, "NewTerminalButton")!;

            AutomationProperties.GetName(button).ShouldBe("New omp terminal");
            ToolTip.GetTip(button).ShouldBe("New omp terminal (Ctrl+Shift+T)");
            button.MinHeight.ShouldBeGreaterThanOrEqualTo(44);
            ViewHarness.TabOrder(main).ToList().IndexOf("New omp terminal").ShouldBe(ViewHarness.TabOrder(main).ToList().IndexOf("New agent") + 1, "the control follows New agent");

            ShellHarness.Click(window, button);
            await ShellWait.UntilAsync(window, () => string.Equals(fixture.Panel.Selected?.Id, "terminal-1", StringComparison.Ordinal), "the terminal session", token).ConfigureAwait(true);

            var rail = ShellHarness.Rows(window);
            ShellTree.VisibleTexts(rail).Count(text => string.Equals(text, "Terminal", StringComparison.Ordinal)).ShouldBe(1, "only the terminal row is tagged");
            fixture.Panel.Selected.ShouldNotBeNull().Row.AccessibleName.ShouldContain(", terminal", Case.Sensitive);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The composer of a terminal is steer-only: its placeholder says so, Send now steers, Queue queues, and the sent text leaves the box.</summary>
    [Fact]
    public async Task TheComposerOfATerminalSteersAndQueuesThroughTheChannel()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var (runtime, _, channel) = await OpenTerminalAsync(fixture, window, token).ConfigureAwait(true);
            var view = (TerminalSteerView)ShellHarness.MainViewOf(window).FindControl<ContentControl>("ComposerHost")!.Content!;
            var input = view.FindControl<TextBox>("MessageInput")!;
            var sendNow = view.FindControl<Button>("SendNowButton")!;
            var queue = view.FindControl<Button>("QueueButton")!;

            input.PlaceholderText.ShouldBe("Steer this omp terminal");
            sendNow.Content.ShouldBe("Send now");
            queue.Content.ShouldBe("Queue");
            AutomationProperties.GetName(input).ShouldBe("Steer message");
            sendNow.MinHeight.ShouldBeGreaterThanOrEqualTo(44);
            queue.MinHeight.ShouldBeGreaterThanOrEqualTo(44);

            input.Text = "turn left";
            ShellHarness.Click(window, sendNow);
            await ShellWait.UntilAsync(window, () => !view.IsWorking && channel.Sent.Count == 1, "the steer to be sent", token).ConfigureAwait(true);

            channel.Sent[0].Mode.ShouldBe(SteeringDirectiveMode.Steer);
            channel.Sent[0].Text.ShouldBe("turn left");
            input.Text.ShouldBeNullOrEmpty();

            input.Text = "then summarise";
            ShellHarness.Click(window, queue);
            await ShellWait.UntilAsync(window, () => !view.IsWorking && channel.Sent.Count == 2, "the follow-up to be sent", token).ConfigureAwait(true);

            channel.Sent[1].Mode.ShouldBe(SteeringDirectiveMode.FollowUp);
            runtime.ViewModel.Steering.History.Select(entry => entry.Mode).ShouldBe([SteeringMode.FollowUp, SteeringMode.Steer]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter in the box sends now, Ctrl+Shift+Enter queues, and a steer the channel refuses keeps its text and says why on the status line.</summary>
    [Fact]
    public async Task AKeyboardSteerThatFailsKeepsItsTextAndShowsTheReason()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var (_, _, channel) = await OpenTerminalAsync(fixture, window, token).ConfigureAwait(true);
            var view = (TerminalSteerView)ShellHarness.MainViewOf(window).FindControl<ContentControl>("ComposerHost")!.Content!;
            var input = view.FindControl<TextBox>("MessageInput")!;
            channel.Unavailable = new SteeringUnavailableException(SteeringFailureReason.NotServing, "The omp session is not answering.");
            input.Text = "turn left";
            input.Focus();

            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await ShellWait.UntilAsync(window, () => !view.IsWorking && ShellHarness.Status(window).IsVisible, "the failure to be shown", token).ConfigureAwait(true);

            input.Text.ShouldBe("turn left", "a steer that was not delivered stays in the box");
            ShellHarness.Status(window).Text.ShouldBe("The omp session is not answering.");
            channel.Sent.ShouldBeEmpty();

            channel.Unavailable = null;
            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter, shift: true, control: true);
            await ShellWait.UntilAsync(window, () => !view.IsWorking && channel.Sent.Count == 1, "the follow-up", token).ConfigureAwait(true);

            channel.Sent.Single().Mode.ShouldBe(SteeringDirectiveMode.FollowUp);
            input.Text.ShouldBeNullOrEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With omp stopped the composer says the terminal is not running and keeps the text.</summary>
    [Fact]
    public async Task ASteerWithOmpStoppedSaysSoAndKeepsTheText()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var (runtime, starter, channel) = await OpenTerminalAsync(fixture, window, token).ConfigureAwait(true);
            starter.Sessions.Single().Exit(0);
            await ShellWait.UntilAsync(window, () => runtime.Terminal!.State == OmpTerminalState.Exited, "omp to exit", token).ConfigureAwait(true);
            var view = (TerminalSteerView)ShellHarness.MainViewOf(window).FindControl<ContentControl>("ComposerHost")!.Content!;
            var input = view.FindControl<TextBox>("MessageInput")!;
            input.Text = "too late";

            ShellHarness.Click(window, view.FindControl<Button>("SendNowButton")!);
            await ShellWait.UntilAsync(window, () => !view.IsWorking && ShellHarness.Status(window).IsVisible, "the status line", token).ConfigureAwait(true);

            ShellHarness.Status(window).Text.ShouldBe(AgentPanelCopy.TerminalNotRunning);
            input.Text.ShouldBe("too late");
            channel.Sent.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Selecting a chat and coming back, and visiting the Kanban tab, show the same terminal view and never restart omp or dispose its session.
    /// </summary>
    [Fact]
    public async Task SwitchingSessionsAndTabsDoesNotRestartOmp()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var starter = new FakeOmpTerminalStarter();
            fixture.AddTerminalSession("terminal-1", "Shell", new SessionScript { TerminalStarter = starter, Steering = new FakeSteeringChannel() });
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;
            fixture.Panel.SelectSession("terminal-1");
            await ShellWait.UntilAsync(window, () => starter.Sizes.Count == 1, "omp to start", token).ConfigureAwait(true);
            ViewHarness.Settle(window);
            var view = main.FindControl<ContentControl>("TranscriptHost")!.Content.ShouldBeOfType<OmpTerminalView>();
            var session = starter.Sessions.Single();

            fixture.Panel.SelectSession("session-1");
            ViewHarness.Settle(window);
            main.FindControl<ContentControl>("TranscriptHost")!.Content.ShouldNotBeSameAs(view);
            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            BoardShell.Chord(window, Key.A, PhysicalKey.A);
            fixture.Panel.SelectSession("terminal-1");
            ViewHarness.Settle(window);

            main.FindControl<ContentControl>("TranscriptHost")!.Content.ShouldBeSameAs(view, "the terminal view is kept with its session");
            starter.Sizes.Count.ShouldBe(1, "omp was not started again");
            session.Disposals.ShouldBe(0, "omp was not stopped by a switch");
            fixture.Runtime("terminal-1").Terminal!.State.ShouldBe(OmpTerminalState.Running);

            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            ViewHarness.Settle(window);

            starter.Sizes.Count.ShouldBe(1);
            session.Disposals.ShouldBe(0, "omp keeps running behind the Kanban tab");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Closing the session ends omp and disposes its session; reopening starts omp again in the same terminal.</summary>
    [Fact]
    public async Task ClosingTheSessionEndsOmpAndReopeningStartsItAgain()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var (runtime, starter, _) = await OpenTerminalAsync(fixture, window, token).ConfigureAwait(true);
            var first = starter.Sessions.Single();

            await fixture.Panel.CloseSessionAsync("terminal-1", token).ConfigureAwait(true);
            await ShellWait.UntilAsync(window, () => first.Disposals == 1, "the terminal session to be disposed", token).ConfigureAwait(true);

            runtime.Terminal!.State.ShouldBe(OmpTerminalState.Idle);
            var main = ShellHarness.MainViewOf(window);
            ShellTree.NamedVisible<Border>(main, "ClosedNotice").ShouldNotBeNull("the closed notice shows");
            main.FindControl<ContentControl>("ComposerHost")!.IsVisible.ShouldBeFalse("a closed terminal has nothing to steer");

            await runtime.ViewModel.ReopenCommand.ExecuteAsync(null).ConfigureAwait(true);
            await ShellWait.UntilAsync(window, () => runtime.Terminal.State == OmpTerminalState.Running, "omp to start again", token).ConfigureAwait(true);

            starter.Sessions.Count.ShouldBe(2, "reopening starts omp once more");
            main.FindControl<ContentControl>("ComposerHost")!.IsVisible.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// With the Kanban tab shown first, a terminal session that is selected has no loaded or started terminal: omp starts once the Agents tab
    /// shows the thread, and no error about a missing emulator is raised.
    /// </summary>
    [Fact]
    public async Task ATerminalSelectedWhileTheKanbanTabIsShownStartsOnlyWhenTheAgentsTabReturns()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddTerminalSession("terminal-1", "Shell");
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;
            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            main.IsKanbanShown.ShouldBeTrue();

            fixture.Panel.SelectSession("terminal-1");
            await ShellWait.UntilAsync(window, () => fixture.Host.Runtimes.ContainsKey("terminal-1"), "the terminal runtime", token).ConfigureAwait(true);
            ViewHarness.Settle(window);
            var (runtime, starter, _) = Parts(fixture, "terminal-1");

            runtime.Terminal!.State.ShouldBe(OmpTerminalState.Idle, "omp does not start behind the Kanban tab");
            starter.Sizes.ShouldBeEmpty();
            main.FindControl<ContentControl>("TranscriptHost")!.Content.ShouldBeOfType<OmpTerminalView>().IsEffectivelyVisible.ShouldBeFalse("its region is hidden behind the Kanban tab");

            BoardShell.Chord(window, Key.A, PhysicalKey.A);
            await ShellWait.UntilAsync(window, () => runtime.Terminal.State == OmpTerminalState.Running, "omp to start once the thread shows", token).ConfigureAwait(true);

            starter.Sizes.Count.ShouldBe(1, "omp starts once");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter on the card of a terminal session opens it in the Agents tab and starts omp once, in a terminal the user can type into.</summary>
    [Fact]
    public async Task EnterOnATerminalCardOpensTheTerminalAndStartsOmp()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddTerminalSession("terminal-1", "Shell");
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;
            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            var card = BoardShell.Board(main).CardViewOf("terminal-1").ShouldNotBeNull();
            card.Model!.Card.KindText.ShouldBe("Terminal");
            card.Focus();

            ViewHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await ShellWait.UntilAsync(window, () => fixture.Host.Runtimes.ContainsKey("terminal-1") && fixture.Runtime("terminal-1").Terminal!.State == OmpTerminalState.Running, "omp to start", token).ConfigureAwait(true);

            main.IsKanbanShown.ShouldBeFalse();
            Parts(fixture, "terminal-1").Starter.Sizes.Count.ShouldBe(1);
            main.FindControl<ContentControl>("TranscriptHost")!.Content.ShouldBeOfType<OmpTerminalView>().IsLoaded.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A terminal session restored closed shows the closed notice and does not start omp when its thread shows; reopening starts omp.</summary>
    [Fact]
    public async Task ATerminalRestoredClosedDoesNotStartOmpUntilItIsReopened()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddTerminalSession("terminal-1", "Shell", lifecycle: SessionLifecycle.Paused);
            fixture.Host.ClosedIds.Add("terminal-1");
            fixture.Catalog.Refresh();
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            fixture.Panel.SelectSession("terminal-1");
            await ShellWait.UntilAsync(window, () => main.FindControl<ContentControl>("TranscriptHost")!.Content is OmpTerminalView { IsLoaded: true }, "the terminal view to load", token).ConfigureAwait(true);
            var (runtime, starter, _) = Parts(fixture, "terminal-1");

            runtime.ViewModel.IsClosed.ShouldBeTrue();
            starter.Sizes.ShouldBeEmpty();
            runtime.Terminal!.State.ShouldBe(OmpTerminalState.Idle);
            ShellTree.NamedVisible<Border>(main, "ClosedNotice").ShouldNotBeNull("the closed notice shows");

            await runtime.ViewModel.ReopenCommand.ExecuteAsync(null).ConfigureAwait(true);
            await ShellWait.UntilAsync(window, () => runtime.Terminal.State == OmpTerminalState.Running, "omp to start after the reopen", token).ConfigureAwait(true);

            starter.Sessions.Count.ShouldBe(1, "reopening starts omp once");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The accessibility walk over a terminal session: the terminal, its status, its buttons and the steer-only composer are all named.</summary>
    [Fact]
    public async Task EveryAuthoredStopOfATerminalSessionHasAName()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            await OpenTerminalAsync(fixture, window, token).ConfigureAwait(true);
            var main = ShellHarness.MainViewOf(window);

            ViewHarness.Unnamed(main).ShouldBeEmpty();
            var order = ViewHarness.TabOrder(main);
            order.ShouldContain("omp terminal", StringComparer.Ordinal);
            order.ShouldContain("Steer message", StringComparer.Ordinal);
            order.ShouldContain("Send now, deliver the message into the running omp", StringComparer.Ordinal);
            order.ShouldContain("Queue, run the message after the current turn", StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Under reduced motion the steer buttons take no transition; under full motion they keep the shared 120 ms background transition.</summary>
    [Fact]
    public async Task ReducedMotionRemovesTheSteerButtonTransitions()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            foreach (var motion in Motions)
            {
                var fixture = ShellFixture.CreateWithSession();
                await using var fixtureScope = fixture.ConfigureAwait(true);
                var window = ShellHarness.Show(fixture, motion);
                await OpenTerminalAsync(fixture, window, token).ConfigureAwait(true);
                var view = (TerminalSteerView)ShellHarness.MainViewOf(window).FindControl<ContentControl>("ComposerHost")!.Content!;

                foreach (var name in SteerButtons)
                {
                    var transitions = view.FindControl<Button>(name)!.Transitions;
                    if (motion == MotionPreference.Reduced)
                    {
                        (transitions?.Count ?? 0).ShouldBe(0, $"{name} under reduced motion");
                    }
                    else
                    {
                        transitions.ShouldNotBeNull().OfType<BrushTransition>().ShouldHaveSingleItem();
                    }
                }
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Every text of the Agents layout with a terminal session, and of the Kanban tab, is drawn in one of the three Greptile families through the OmpOS.Font tokens.</summary>
    [Fact]
    public async Task EveryTextIsDrawnInAFontFromTheTokens()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddTerminalSession("terminal-1", "Shell");
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;
            fixture.Panel.SelectSession("terminal-1");
            await ShellWait.UntilAsync(window, () => fixture.Host.Runtimes.ContainsKey("terminal-1"), "the terminal runtime", token).ConfigureAwait(true);
            ViewHarness.Settle(window);
            var families = FontTokens
                .Select(key => ((FontFamily)Application.Current!.FindResource(key)!).Name)
                .ToHashSet(StringComparer.Ordinal);

            Fonts(main).Except(families, StringComparer.Ordinal).ShouldBeEmpty("the Agents tab draws only token fonts");

            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            ViewHarness.Settle(window);

            Fonts(main).Except(families, StringComparer.Ordinal).ShouldBeEmpty("the Kanban tab draws only token fonts");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static HashSet<string> Fonts(Control root) =>
        root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(static block => block.IsEffectivelyVisible && !string.IsNullOrEmpty(block.Text))
            .Select(static block => block.FontFamily.Name)
            .ToHashSet(StringComparer.Ordinal);

    private static (AgentSessionRuntime Runtime, FakeOmpTerminalStarter Starter, FakeSteeringChannel Channel) Parts(ShellFixture fixture, string id)
    {
        var script = fixture.Script(id);
        return (fixture.Runtime(id), (FakeOmpTerminalStarter)script.TerminalStarter!, (FakeSteeringChannel)script.Steering!);
    }

    private static async Task<(AgentSessionRuntime Runtime, FakeOmpTerminalStarter Starter, FakeSteeringChannel Channel)> OpenTerminalAsync(
        ShellFixture fixture,
        MainWindow window,
        CancellationToken token)
    {
        ShellHarness.Press(window, Key.T, PhysicalKey.T, shift: true, control: true);
        await ShellWait.UntilAsync(window, () => string.Equals(fixture.Panel.Selected?.Id, "terminal-1", StringComparison.Ordinal), "the terminal session", token).ConfigureAwait(true);
        var parts = Parts(fixture, "terminal-1");
        await ShellWait.UntilAsync(window, () => parts.Runtime.Terminal!.State == OmpTerminalState.Running, "omp to start", token).ConfigureAwait(true);
        return parts;
    }
}
