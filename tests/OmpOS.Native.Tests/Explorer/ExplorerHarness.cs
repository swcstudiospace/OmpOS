using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using OmpOS.Native.Views.Explorer;
using OmpOS.Presentation.Editor;

namespace OmpOS.Native.Tests.Explorer;

/// <summary>Shared helpers for the explorer tests: builds the fixture folder, shows the explorer and reads and presses its rows.</summary>
internal static class ExplorerHarness
{
    /// <summary>The absolute root path of the fixture folder.</summary>
    public const string Root = "/repo/workspace";

    /// <summary>Creates the fixture folder: <c>README.md</c>, <c>docs/readme.md</c>, <c>src/main.cs</c> and <c>src/lib/util.cs</c>.</summary>
    /// <returns>The file system.</returns>
    public static ListingWorkspaceFileSystem NewFolder() =>
        new ListingWorkspaceFileSystem(Root)
            .WithFile("README.md")
            .WithFile("docs/readme.md")
            .WithFile("src/main.cs")
            .WithFile("src/lib/util.cs");

    /// <summary>Shows an explorer view bound to the tree in an active window, and gives it the layout passes a real window gets.</summary>
    /// <param name="tree">The tree to bind.</param>
    /// <returns>The shown window.</returns>
    public static ExplorerWindow Show(WorkspaceTree tree)
    {
        var window = new ExplorerWindow
        {
            Width = 600,
            Height = 700,
            Content = new WorkspaceTreeView { DataContext = tree },
        };
        window.Show();
        window.Activate();
        ShellHarness.Settle(window);
        return window;
    }

    /// <summary>Gets the explorer view the window shows.</summary>
    public static WorkspaceTreeView View(Window window) => (WorkspaceTreeView)window.Content!;

    /// <summary>Gets the list of visible rows.</summary>
    public static ListBox Rows(Window window) => View(window).FindControl<ListBox>("TreeRows")!;

    /// <summary>Gets the names the rows draw, in list order: the accessible name of each row's text, read from its container.</summary>
    public static string[] RenderedNames(Window window)
    {
        var rows = Rows(window);
        return Enumerable.Range(0, rows.ItemCount)
            .Select(index => rows.ContainerFromIndex(index)!.GetVisualDescendants().OfType<TextBlock>().First())
            .Select(text => AutomationProperties.GetName(text) ?? string.Empty)
            .ToArray();
    }

    /// <summary>Gets the name of the selected row, or null when no row is selected.</summary>
    public static string? SelectedName(Window window) => (Rows(window).SelectedItem as WorkspaceRow)?.Name;

    /// <summary>Gives the row list the keyboard focus, as a user who tabbed to it would have.</summary>
    /// <param name="window">The shown window.</param>
    public static void FocusRows(Window window) => Rows(window).Focus();

    /// <summary>Presses and releases one key on the window, as the keyboard would, and settles the layout.</summary>
    /// <param name="window">The shown window that owns the focus.</param>
    /// <param name="key">The logical key.</param>
    /// <param name="physical">The physical key.</param>
    public static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, default, physical, null);
        ShellHarness.Settle(window);
    }
}
