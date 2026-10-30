using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Tests.Parity;
using Autonogrammer.Presentation.AgentPanel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shouldly;

namespace Autonogrammer.Native.Tests.Shortcuts;

/// <summary>
/// What each gesture of the main window, the agent list and the composer does, observed in a headless shell. Each method raises the gesture
/// of the catalogue entry it is given and asserts the effect the entry promises; none asserts a shortcut by reading the handler.
/// </summary>
internal static class ShellShortcutEffects
{
    private static readonly ScriptedTurn Working = new([new TextDeltaEvent("working")], HoldUntilCancelled: true);

    /// <summary>Ctrl+, asks for the Settings window.</summary>
    internal static void OpenSettings(KeyGesture gesture)
    {
        var window = ShellHarness.Show();
        var main = ShellHarness.MainViewOf(window);
        var raised = 0;
        main.SettingsRequested += (_, _) => raised++;

        ShortcutKeys.Press(window, gesture);

        raised.ShouldBe(1);
    }

    /// <summary>F6 and Shift+F6 move focus from the thread to the rail.</summary>
    internal static void RegionFocusMoves(KeyGesture gesture)
    {
        var window = ShellHarness.Show();
        ShellHarness.Prompt(window).Focus();

        ShortcutKeys.Press(window, gesture);

        FocusIn(window, "RailRegion").ShouldBeTrue("focus moved from the thread to the rail");
    }

    /// <summary>Ctrl+1 moves focus into the rail.</summary>
    internal static void FocusAgentList(KeyGesture gesture)
    {
        var window = ShellHarness.Show();
        ShellHarness.Prompt(window).Focus();

        ShortcutKeys.Press(window, gesture);

        FocusIn(window, "RailRegion").ShouldBeTrue();
        ShellHarness.Prompt(window).IsFocused.ShouldBeFalse();
    }

    /// <summary>Ctrl+2 closes an open tool and focuses the prompt.</summary>
    internal static void FocusPrompt(KeyGesture gesture)
    {
        var window = ShellHarness.Show();
        var main = ShellHarness.MainViewOf(window);
        ShellHarness.ShowToolsView(window);
        main.FindControl<ToggleButton>("EditorToggle")!.IsChecked = true;
        ShellHarness.Settle(window);
        main.FindControl<ContentControl>("ToolHost")!.IsVisible.ShouldBeTrue();

        ShortcutKeys.Press(window, gesture);

        main.FindControl<ContentControl>("ToolHost")!.IsVisible.ShouldBeFalse();
        ShellHarness.Prompt(window).IsFocused.ShouldBeTrue();
    }

    /// <summary>Ctrl+F opens the transcript search and focuses its box.</summary>
    internal static void SearchTranscript(KeyGesture gesture)
    {
        var window = ShellHarness.Show();
        var searchHost = ShellHarness.MainViewOf(window).FindControl<ContentControl>("SearchHost")!;
        searchHost.IsVisible.ShouldBeFalse();
        ShellHarness.Prompt(window).Focus();

        ShortcutKeys.Press(window, gesture);

        searchHost.IsVisible.ShouldBeTrue();
        ShellTree.Named<TextBox>(searchHost, "SearchInput")!.IsFocused.ShouldBeTrue();
    }

    /// <summary>Ctrl+K opens the search over all conversations, and the same gesture closes it again.</summary>
    internal static void SearchConversations(KeyGesture gesture)
    {
        var window = ShellHarness.Show();
        var search = ShellHarness.MainViewOf(window).ConversationSearch.ShouldNotBeNull();
        search.IsOpen.ShouldBeFalse();
        ShellHarness.Prompt(window).Focus();

        ShortcutKeys.Press(window, gesture);
        search.IsOpen.ShouldBeTrue();

        ShortcutKeys.Press(window, gesture);
        search.IsOpen.ShouldBeFalse();
    }

    /// <summary>Esc closes the transcript search and returns focus to the transcript.</summary>
    internal static void Dismiss(KeyGesture gesture, KeyGesture openSearch)
    {
        var window = ShellHarness.Show();
        var searchHost = ShellHarness.MainViewOf(window).FindControl<ContentControl>("SearchHost")!;
        ShellHarness.Prompt(window).Focus();
        ShortcutKeys.Press(window, openSearch);
        searchHost.IsVisible.ShouldBeTrue("the search is open before Esc");

        ShortcutKeys.Press(window, gesture);

        searchHost.IsVisible.ShouldBeFalse();
        ShellHarness.Scroll(window).IsFocused.ShouldBeTrue();
    }

    /// <summary>Ctrl+N creates an agent, selects it and focuses its prompt.</summary>
    internal static async Task NewAgentAsync(KeyGesture gesture, CancellationToken cancellationToken)
    {
        var fixture = ShellFixture.CreateWithSession();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var window = ShellHarness.Show(fixture);
        ShellHarness.ShowToolsView(window);
        ShellHarness.MainViewOf(window).FindControl<ToggleButton>("AgentsToggle")!.Focus();

        ShortcutKeys.Press(window, gesture);
        await ShellWait.UntilAsync(window, () => string.Equals(fixture.Panel.Selected?.Id, "new-1", StringComparison.Ordinal), "the new agent to be selected", cancellationToken).ConfigureAwait(true);

        fixture.Host.Runtimes.ShouldContainKey("new-1");
        ShellHarness.Prompt(window).IsFocused.ShouldBeTrue();
    }

    /// <summary>Ctrl+Shift+T creates a terminal agent and selects it.</summary>
    internal static async Task NewTerminalAsync(KeyGesture gesture, CancellationToken cancellationToken)
    {
        var fixture = ShellFixture.CreateWithSession();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var window = ShellHarness.Show(fixture);

        ShortcutKeys.Press(window, gesture);
        await ShellWait.UntilAsync(window, () => string.Equals(fixture.Panel.Selected?.Id, "terminal-1", StringComparison.Ordinal), "the terminal session", cancellationToken).ConfigureAwait(true);

        fixture.Host.CreatedTerminals.ShouldBe(["terminal-1"]);
    }

    /// <summary>Ctrl+Shift+A brings the Agents tab back from the Kanban tab and focuses the prompt.</summary>
    internal static async Task ShowAgentsTabAsync(KeyGesture gesture, CancellationToken cancellationToken)
    {
        var fixture = ShellFixture.CreateWithSession();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var main = fixture.ShowWithBoard();
        var window = fixture.BoardWindow!;
        BoardShell.Chord(window, Key.K, PhysicalKey.K);
        main.IsKanbanShown.ShouldBeTrue("the Kanban tab is shown before the gesture");

        ShortcutKeys.Press(window, gesture);

        main.IsKanbanShown.ShouldBeFalse();
        BoardShell.Region(main, "ThreadRegion").IsVisible.ShouldBeTrue();
        var prompt = ShellTree.Named<TextBox>(main, "PromptInput")!;
        await ShellWait.UntilAsync(window, () => prompt.IsFocused, "the prompt to take focus", cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Ctrl+Shift+K shows the Kanban tab in place of the rail and the thread.</summary>
    internal static async Task ShowKanbanTabAsync(KeyGesture gesture)
    {
        var fixture = ShellFixture.CreateWithSession();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var main = fixture.ShowWithBoard();
        main.IsKanbanShown.ShouldBeFalse();

        ShortcutKeys.Press(fixture.BoardWindow!, gesture);

        main.IsKanbanShown.ShouldBeTrue();
        BoardShell.Region(main, "KanbanHost").IsVisible.ShouldBeTrue();
        BoardShell.Region(main, "RailRegion").IsVisible.ShouldBeFalse();
        BoardShell.Region(main, "ThreadRegion").IsVisible.ShouldBeFalse();
    }

    /// <summary>Enter in the agent list focuses the selected agent's prompt.</summary>
    internal static void OpenAgent(KeyGesture gesture, KeyGesture focusList)
    {
        var window = ShellHarness.Show();
        ShortcutKeys.Press(window, focusList);
        ShellHarness.Prompt(window).IsFocused.ShouldBeFalse("the list has the focus before Enter");

        ShortcutKeys.Press(window, gesture);

        ShellHarness.Prompt(window).IsFocused.ShouldBeTrue();
    }

    /// <summary>Delete in the agent list closes the selected agent at once when it is idle.</summary>
    internal static async Task CloseAgentAsync(KeyGesture gesture, KeyGesture focusList, CancellationToken cancellationToken)
    {
        var fixture = ShellFixture.CreateWithSession();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        fixture.AddSession("session-2", "Agent two");
        var window = ShellHarness.Show(fixture);
        ShortcutKeys.Press(window, focusList);

        ShortcutKeys.Press(window, gesture);
        await ShellWait.UntilAsync(window, () => fixture.Host.Closed.Count == 1, "the idle agent to close", cancellationToken).ConfigureAwait(true);

        fixture.Host.Closed.ShouldBe(["session-1"]);
    }

    /// <summary>Enter in the prompt sends the message: the run starts and the draft clears. Shift+Enter is left to the text box.</summary>
    internal static async Task SendAsync(KeyGesture gesture, CancellationToken cancellationToken)
    {
        var fixture = ShellFixture.CreateWithSession(SessionScript.WithTurns(Working));
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var window = ShellHarness.Show(fixture);
        var runtime = fixture.Runtime("session-1");
        ShellHarness.Prompt(window).Text = "long task";
        ShellHarness.Prompt(window).Focus();

        ShortcutKeys.Press(window, gesture);
        await ShellHarness.DrainAsync(window).ConfigureAwait(true);

        runtime.Composer.Draft.ShouldBeNullOrEmpty("the text box inserted no line after the send");
        runtime.Composer.IsStreaming.ShouldBeTrue("the message was sent and the run started");
        ShellHarness.Prompt(window).Text.ShouldBeNullOrEmpty();

        await runtime.Composer.StopAsync().ConfigureAwait(true);
        await ShellWait.RunEndedAsync(window, runtime, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Shift+Enter sends nothing and leaves the key to the text box, which adds the line.</summary>
    internal static async Task NewLineAsync(KeyGesture gesture)
    {
        var fixture = ShellFixture.CreateWithSession(SessionScript.WithTurns(Working));
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var window = ShellHarness.Show(fixture);
        var runtime = fixture.Runtime("session-1");
        ShellHarness.Prompt(window).Text = "hello";
        ShellHarness.Prompt(window).CaretIndex = "hello".Length;
        ShellHarness.Prompt(window).Focus();

        ShortcutKeys.Press(window, gesture);
        await ShellHarness.DrainAsync(window).ConfigureAwait(true);

        runtime.Composer.IsStreaming.ShouldBeFalse("nothing was sent");
        var text = ShellHarness.Prompt(window).Text.ShouldNotBeNull();
        text.ShouldStartWith("hello");
        text.ShouldContain("\n", customMessage: "the text box added the line break");
    }

    /// <summary>Ctrl+Enter steers the active run and Ctrl+Shift+Enter queues a follow-up; each enters the steering history in its own mode.</summary>
    internal static async Task SteeringAsync(KeyGesture gesture, SteeringMode mode, CancellationToken cancellationToken)
    {
        var fixture = ShellFixture.CreateWithSession(SessionScript.WithTurns(Working, new ScriptedTurn([new CompletedEvent("end_turn")])));
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var window = ShellHarness.Show(fixture);
        var runtime = fixture.Runtime("session-1");
        ShellHarness.Prompt(window).Text = "long task";
        ShellHarness.Prompt(window).Focus();
        ShortcutKeys.Press(window, new KeyGesture(Key.Enter));
        await ShellHarness.DrainAsync(window).ConfigureAwait(true);
        runtime.Composer.IsStreaming.ShouldBeTrue("a run is active before the gesture");

        ShellHarness.Prompt(window).Text = "next message";
        ShellHarness.Prompt(window).Focus();
        ShortcutKeys.Press(window, gesture);
        await ShellHarness.DrainAsync(window).ConfigureAwait(true);

        runtime.ViewModel.Steering.History.Select(entry => (entry.Mode, entry.Text)).ShouldBe([(mode, "next message")]);
        ShellHarness.Prompt(window).Text.ShouldBeNullOrEmpty("the draft clears once the message is accepted");

        await runtime.Composer.StopAsync().ConfigureAwait(true);
        await ShellWait.RunEndedAsync(window, runtime, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Esc in the prompt while a run is active asks before it stops; nothing is stopped by the key alone.</summary>
    internal static async Task StopRunAsync(KeyGesture gesture, CancellationToken cancellationToken)
    {
        var fixture = ShellFixture.CreateWithSession(SessionScript.WithTurns(Working));
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var window = ShellHarness.Show(fixture);
        var main = ShellHarness.MainViewOf(window);
        var runtime = fixture.Runtime("session-1");
        var channel = fixture.Script("session-1").Scripted;
        ShellHarness.Prompt(window).Text = "long task";
        ShellHarness.Prompt(window).Focus();
        ShortcutKeys.Press(window, new KeyGesture(Key.Enter));
        await ShellHarness.DrainAsync(window).ConfigureAwait(true);
        ShellTree.NamedVisible<Border>(main, "StopConfirmBar").ShouldBeNull("nothing asks before the gesture");

        ShortcutKeys.Press(window, gesture);

        ShellTree.NamedVisible<Border>(main, "StopConfirmBar").ShouldNotBeNull("Esc asks first");
        channel.CancelCount.ShouldBe(0, "the key alone stops nothing");
        runtime.Composer.IsStreaming.ShouldBeTrue();

        await runtime.Composer.StopAsync().ConfigureAwait(true);
        await ShellWait.RunEndedAsync(window, runtime, cancellationToken).ConfigureAwait(true);
    }

    private static bool FocusIn(MainWindow window, string regionName)
    {
        var focused = window.FocusManager?.GetFocusedElement() as Visual;
        var region = ShellHarness.MainViewOf(window).FindControl<Control>(regionName)!;
        return focused is not null && (ReferenceEquals(focused, region) || region.IsVisualAncestorOf(focused));
    }
}
