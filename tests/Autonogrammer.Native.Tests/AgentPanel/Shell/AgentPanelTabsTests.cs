using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Presentation;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>
/// The top-level tabs of the main view: Agents and Kanban exist only when the head supplies a board, are named and ordered after the skip
/// link, switch the whole area without losing the Agents layout, and have the keyboard map Ctrl+Shift+A and Ctrl+Shift+K.
/// </summary>
public sealed class AgentPanelTabsTests
{
    /// <summary>A main view built with no board has no tab strip: the Agents layout is all there is, and the shortcuts do nothing.</summary>
    [Fact]
    public async Task WithNoBoardThereAreNoTabs()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);

            BoardShell.Tabs(main).IsVisible.ShouldBeFalse();
            ViewHarness.TabOrder(main).ShouldNotContain("Kanban", StringComparer.Ordinal);

            ShellHarness.Press(window, Key.K, PhysicalKey.K, shift: true, control: true);

            main.IsKanbanShown.ShouldBeFalse();
            BoardShell.Region(main, "ThreadRegion").IsVisible.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The two tabs are named Agents and Kanban, Agents is selected first, and the strip is the first stop after the skip link.</summary>
    [Fact]
    public async Task TheTabsAreNamedAndComeAfterTheSkipLink()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithBoard();

            var tabs = BoardShell.Tabs(main);
            tabs.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(BoardShell.AgentsTab(main)).ShouldBe("Agents");
            AutomationProperties.GetName(BoardShell.KanbanTab(main)).ShouldBe("Kanban");
            BoardShell.AgentsTab(main).IsSelected.ShouldBeTrue();
            BoardShell.KanbanTab(main).IsSelected.ShouldBeFalse();
            main.IsKanbanShown.ShouldBeFalse();

            var order = ViewHarness.TabOrder(main).ToList();
            order[0].ShouldBe("Skip to conversation");
            order[1].ShouldBe("Agents");
            order.IndexOf("Sessions view").ShouldBeGreaterThan(1, "the rail follows the tabs");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Ctrl+Shift+K opens the Kanban tab in place of the rail and the thread, and Ctrl+Shift+A brings the Agents layout back with focus in its prompt.</summary>
    [Fact]
    public async Task CtrlShiftKOpensKanbanAndCtrlShiftAOpensAgents()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;

            BoardShell.Chord(window, Key.K, PhysicalKey.K);

            main.IsKanbanShown.ShouldBeTrue();
            BoardShell.KanbanTab(main).IsSelected.ShouldBeTrue();
            BoardShell.Region(main, "KanbanHost").IsVisible.ShouldBeTrue();
            BoardShell.Region(main, "RailRegion").IsVisible.ShouldBeFalse();
            BoardShell.Region(main, "ThreadRegion").IsVisible.ShouldBeFalse();
            BoardShell.Board(main).Board.ShouldBeSameAs(fixture.Board);
            ViewHarness.TabOrder(main).ShouldNotContain("Sessions view", StringComparer.Ordinal, "the rail is not a tab stop while the board is shown");

            BoardShell.Chord(window, Key.A, PhysicalKey.A);

            main.IsKanbanShown.ShouldBeFalse();
            BoardShell.AgentsTab(main).IsSelected.ShouldBeTrue();
            BoardShell.Region(main, "KanbanHost").IsVisible.ShouldBeFalse();
            BoardShell.Region(main, "RailRegion").IsVisible.ShouldBeTrue();
            BoardShell.Region(main, "ThreadRegion").IsVisible.ShouldBeTrue();
            var prompt = ShellTree.Named<TextBox>(main, "PromptInput")!;
            await ShellWait.UntilAsync(window, () => prompt.IsFocused, "the prompt to take focus", token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Choosing a tab in the strip, as a pointer click does, switches the layout, and choosing Agents again brings it back.</summary>
    [Fact]
    public async Task SelectingATabInTheStripSwitchesTheLayout()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;

            BoardShell.Tabs(main).SelectedItem = BoardShell.KanbanTab(main);
            ViewHarness.Settle(window);

            main.IsKanbanShown.ShouldBeTrue("selecting the Kanban tab shows the board");
            BoardShell.Region(main, "KanbanHost").IsVisible.ShouldBeTrue();
            BoardShell.Region(main, "ThreadRegion").IsVisible.ShouldBeFalse();

            BoardShell.Tabs(main).SelectedItem = BoardShell.AgentsTab(main);
            ViewHarness.Drain();
            ViewHarness.Settle(window);

            main.IsKanbanShown.ShouldBeFalse();
            BoardShell.Region(main, "ThreadRegion").IsVisible.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Switching tabs keeps the selected session, its draft and its engine: nothing in the Agents layout is rebuilt.</summary>
    [Fact]
    public async Task SwitchingTabsKeepsTheSessionAndItsDraft()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("session-2", "Agent two");
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;
            var runtime = fixture.Runtime("session-1");
            var prompt = ShellTree.Named<TextBox>(main, "PromptInput")!;
            prompt.Text = "half a thought";

            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            BoardShell.Chord(window, Key.A, PhysicalKey.A);

            fixture.Panel.Selected.ShouldNotBeNull().Id.ShouldBe("session-1");
            ShellTree.Named<TextBox>(main, "PromptInput").ShouldBeSameAs(prompt);
            prompt.Text.ShouldBe("half a thought");
            fixture.Host.Runtimes.Keys.ShouldBe(["session-1"], Case.Sensitive, "no session was opened or restarted by the tab switch");
            fixture.Runtime("session-1").ShouldBeSameAs(runtime);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The shortcuts that start a session work from the Kanban tab and bring the Agents layout back first.</summary>
    [Fact]
    public async Task CtrlNAndCtrlShiftTStartASessionAndShowTheAgentsLayout()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;

            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            ViewHarness.Press(window, Key.N, PhysicalKey.N, RawInputModifiers.Control);
            await ShellWait.UntilAsync(window, () => string.Equals(fixture.Panel.Selected?.Id, "new-1", StringComparison.Ordinal), "the new agent", token).ConfigureAwait(true);

            main.IsKanbanShown.ShouldBeFalse("New agent shows the thread it creates");

            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            BoardShell.Chord(window, Key.T, PhysicalKey.T);
            await ShellWait.UntilAsync(window, () => fixture.Host.CreatedTerminals.Count == 1, "the new terminal", token).ConfigureAwait(true);

            main.IsKanbanShown.ShouldBeFalse("New omp terminal shows the thread it creates");
            fixture.Panel.Selected.ShouldNotBeNull().Id.ShouldBe(fixture.Host.CreatedTerminals[0]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>F6 on the Kanban tab moves focus between the tab strip and the board, and every authored stop of both tabs has a name.</summary>
    [Fact]
    public async Task BothTabsHaveNamedStopsAndF6MovesBetweenTheStripAndTheBoard()
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

            ViewHarness.Unnamed(main).ShouldBeEmpty("the Agents tab");

            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            ViewHarness.Unnamed(main).ShouldBeEmpty("the Kanban tab");
            await ShellWait.UntilAsync(window, () => BoardShell.FocusIn(window, BoardShell.Region(main, "KanbanHost")), "focus on the board", token).ConfigureAwait(true);

            ViewHarness.Press(window, Key.F6, PhysicalKey.F6);
            BoardShell.FocusIn(window, BoardShell.Tabs(main)).ShouldBeTrue("F6 moves from the board to the tab strip");

            ViewHarness.Press(window, Key.F6, PhysicalKey.F6);
            await ShellWait.UntilAsync(window, () => BoardShell.FocusIn(window, BoardShell.Region(main, "KanbanHost")), "focus back on the board", token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Under reduced motion the tabs take no transition, and the view carries the class the head's styles read.</summary>
    [Fact]
    public async Task ReducedMotionRemovesTheTabTransitions()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var reduced = ShellFixture.CreateWithSession();
            await using var reducedScope = reduced.ConfigureAwait(true);
            var main = reduced.ShowWithBoard(MotionPreference.Reduced);

            main.Classes.Contains(MainView.ReducedMotionClass).ShouldBeTrue();
            foreach (Animatable tab in new Animatable[] { BoardShell.Tabs(main), BoardShell.AgentsTab(main), BoardShell.KanbanTab(main) })
            {
                (tab.Transitions?.Count ?? 0).ShouldBe(0);
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
