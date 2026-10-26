using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using OmpOS.Native.Tests.AgentPanel.Views;
using OmpOS.Native.Views.Kanban;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>Helpers for the tests of the main view with a Kanban board: the tab strip, the board view, the keyboard chords and focus.</summary>
internal static class BoardShell
{
    /// <summary>The modifiers of the tab shortcuts: Ctrl and Shift.</summary>
    internal const RawInputModifiers CtrlShift = RawInputModifiers.Control | RawInputModifiers.Shift;

    /// <summary>Gets the top-level tab strip of the main view.</summary>
    /// <param name="main">The main view.</param>
    /// <returns>The strip.</returns>
    internal static TabStrip Tabs(MainView main) => main.FindControl<TabStrip>("TopTabs")!;

    /// <summary>Gets the Agents tab.</summary>
    /// <param name="main">The main view.</param>
    /// <returns>The tab.</returns>
    internal static TabStripItem AgentsTab(MainView main) => main.FindControl<TabStripItem>("AgentsTab")!;

    /// <summary>Gets the Kanban tab.</summary>
    /// <param name="main">The main view.</param>
    /// <returns>The tab.</returns>
    internal static TabStripItem KanbanTab(MainView main) => main.FindControl<TabStripItem>("KanbanTab")!;

    /// <summary>Gets the Kanban board view the Kanban tab hosts.</summary>
    /// <param name="main">The main view.</param>
    /// <returns>The board view.</returns>
    internal static KanbanBoardView Board(MainView main) => (KanbanBoardView)main.FindControl<ContentControl>("KanbanHost")!.Content!;

    /// <summary>Gets a region of the main view by its name: the rail, the thread or the Kanban host.</summary>
    /// <param name="main">The main view.</param>
    /// <param name="name">The region's <c>x:Name</c>.</param>
    /// <returns>The region.</returns>
    internal static Control Region(MainView main, string name) => main.FindControl<Control>(name)!;

    /// <summary>Says whether keyboard focus is on a control inside a region, or on the region itself.</summary>
    /// <param name="window">The window that owns the focus.</param>
    /// <param name="region">The region.</param>
    /// <returns>True when focus is within the region.</returns>
    internal static bool FocusIn(Window window, Control region)
    {
        var focused = window.FocusManager?.GetFocusedElement() as Visual;
        return focused is not null && (ReferenceEquals(focused, region) || region.IsVisualAncestorOf(focused));
    }

    /// <summary>Presses Ctrl+Shift and a letter, as the keyboard would, then settles the window.</summary>
    /// <param name="window">The window that owns the focus.</param>
    /// <param name="key">The logical key.</param>
    /// <param name="physical">The physical key.</param>
    internal static void Chord(Window window, Key key, PhysicalKey physical) => ViewHarness.Press(window, key, physical, CtrlShift);
}
