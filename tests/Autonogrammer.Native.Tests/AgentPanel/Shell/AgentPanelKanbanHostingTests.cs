using Autonogrammer.Contracts.Agents;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Tests.Terminal;
using Autonogrammer.Native.Views;
using Avalonia.Controls;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>
/// The Kanban tab as the main view hosts it: the board of the head sits in the tab, a card opens its session in the Agents layout, the empty
/// board starts a session, and the task chip of a thread header leads to the card of that session.
/// </summary>
public sealed class AgentPanelKanbanHostingTests
{
    /// <summary>Every session of the catalog is a card on the hosted board, and a session created later appears without a refresh.</summary>
    [Fact]
    public async Task EachSessionIsACardAndANewOneAppearsLive()
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

            var board = BoardShell.Board(main);
            board.CardViewOf("session-1").ShouldNotBeNull();
            board.CardViewOf("terminal-1").ShouldNotBeNull().Model!.Card.KindText.ShouldBe("Terminal");

            fixture.AddSession("session-2", "Agent two");
            await ShellWait.UntilAsync(window, () => board.CardViewOf("session-2") is not null, "the new card", token).ConfigureAwait(true);

            board.CardViews.Count.ShouldBe(3);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter on a card selects its session in the panel, switches to the Agents tab and puts focus in that session's prompt.</summary>
    [Fact]
    public async Task EnterOnACardOpensItsSessionAndSwitchesToTheAgentsTab()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("session-2", "Agent two");
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;
            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            var card = BoardShell.Board(main).CardViewOf("session-2").ShouldNotBeNull();
            card.Focus();

            ViewHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await ShellWait.UntilAsync(window, () => string.Equals(fixture.Panel.Selected?.Id, "session-2", StringComparison.Ordinal), "session two to be selected", token).ConfigureAwait(true);

            main.IsKanbanShown.ShouldBeFalse();
            BoardShell.Region(main, "ThreadRegion").IsVisible.ShouldBeTrue();
            var composer = (ComposerView)main.FindControl<ContentControl>("ComposerHost")!.Content!;
            composer.DataContext.ShouldBeSameAs(fixture.Runtime("session-2").Composer);
            await ShellWait.UntilAsync(window, () => composer.FindControl<TextBox>("PromptInput")!.IsFocused, "the prompt to take focus", token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The empty board's start button creates a session like New agent, and the thread of the new session is shown.</summary>
    [Fact]
    public async Task TheEmptyBoardStartButtonCreatesASession()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;
            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            var board = BoardShell.Board(main);

            fixture.Board!.IsEmpty.ShouldBeTrue();
            var start = board.StartSession;
            start.IsEffectivelyVisible.ShouldBeTrue("the empty board offers a way to start a session");

            ViewHarness.Click(window, start);
            await ShellWait.UntilAsync(window, () => string.Equals(fixture.Panel.Selected?.Id, "new-1", StringComparison.Ordinal), "the new agent to be selected", token).ConfigureAwait(true);

            fixture.Host.Runtimes.ShouldContainKey("new-1");
            main.IsKanbanShown.ShouldBeFalse();
            BoardShell.Region(main, "ThreadRegion").IsVisible.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The task chip of the thread header is no longer a dead control: it opens the Kanban tab with focus on the card of its session.</summary>
    [Fact]
    public async Task TheTaskChipLeadsToTheCardOfItsSession()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var script = new SessionScript
            {
                Context = new ScriptedTaskContext(new TaskContext
                {
                    Title = "Ship the panel",
                    TaskId = "T-1",
                    SharedMemory = [],
                    Lessons = [],
                    Warnings = [],
                }),
            };
            var fixture = ShellFixture.CreateWithSession(script);
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("session-2", "Agent two");
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;
            await ShellWait.UntilAsync(window, () => ShellTree.NamedVisible<Button>(main, "TaskChipButton") is not null, "the task chip", token).ConfigureAwait(true);

            ViewHarness.Click(window, ShellTree.NamedVisible<Button>(main, "TaskChipButton")!);

            main.IsKanbanShown.ShouldBeTrue("the chip switches to the Kanban tab");
            var card = BoardShell.Board(main).CardViewOf("session-1").ShouldNotBeNull();
            await ShellWait.UntilAsync(window, () => card.IsFocused, "the card of the session to take focus", token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A terminal session that works on a task has a task chip that leads to its card, and that card is the Terminal kind, not a chat card.</summary>
    [Fact]
    public async Task TheTaskChipOfATerminalSessionLeadsToACardOfKindTerminal()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var script = new SessionScript
            {
                TerminalStarter = new FakeOmpTerminalStarter(),
                Steering = new FakeSteeringChannel(),
                Context = new ScriptedTaskContext(new TaskContext
                {
                    Title = "Ship the panel",
                    TaskId = "T-1",
                    SharedMemory = [],
                    Lessons = [],
                    Warnings = [],
                }),
            };
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddTerminalSession("terminal-1", "Shell", script, taskId: "T-1");
            fixture.Panel.SelectSession("terminal-1");
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;
            await ShellWait.UntilAsync(window, () => ShellTree.NamedVisible<Button>(main, "TaskChipButton") is not null, "the task chip", token).ConfigureAwait(true);

            ViewHarness.Click(window, ShellTree.NamedVisible<Button>(main, "TaskChipButton")!);

            main.IsKanbanShown.ShouldBeTrue("the chip switches to the Kanban tab");
            var card = BoardShell.Board(main).CardViewOf("terminal-1").ShouldNotBeNull();
            card.Model!.Card.KindText.ShouldBe("Terminal");
            card.Model.Card.Kind.ShouldBe(SessionKind.Terminal);
            await ShellWait.UntilAsync(window, () => card.IsFocused, "the card of the terminal session to take focus", token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The board view is the one the head built over its model: closing the main view releases its subscriptions and leaves the board alive.</summary>
    [Fact]
    public async Task ClosingTheViewLeavesTheHeadsBoardAlive()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("session-2", "Agent two");
            var main = fixture.ShowWithBoard();
            var board = fixture.Board!;
            var opened = 0;
            board.OpenSessionRequested += (_, _) => opened++;

            main.Close();
            board.RequestOpen("session-2").ShouldBeTrue("the board still raises its events for its owner");

            opened.ShouldBe(1);
            fixture.Panel.Selected.ShouldNotBeNull().Id.ShouldBe("session-1", "the closed view no longer follows the board");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
