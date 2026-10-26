using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using OmpOS.Native.Views.Explorer;
using OmpOS.Presentation.Editor;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Explorer;

/// <summary>
/// The workspace explorer: a directory lists its children when it expands, every row is reachable and moved between by the
/// keyboard, and the open-folder root is readable by the orchestrator (ADE-10, XP-01).
/// </summary>
/// <remarks>
/// The tree lists directories before files, each group by name. The fixture root therefore reads <c>docs</c>, <c>src</c>,
/// <c>README.md</c>, and it opens with <c>docs</c> selected.
/// </remarks>
public sealed class WorkspaceTreeViewTests
{
    private static readonly string[] RootRows = ["docs", "src", "README.md"];

    /// <summary>Opening a folder lists only the root; a directory's children are listed when it first expands.</summary>
    [Fact]
    public async Task ChildrenLoadWhenADirectoryExpands()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var folder = ExplorerHarness.NewFolder();
            var tree = new WorkspaceTree();
            using var window = ExplorerHarness.Show(tree);
            tree.OpenFolder(folder);
            ShellHarness.Settle(window);

            folder.ListedDirectories.ShouldBe([""]);

            ExplorerHarness.FocusRows(window);
            ExplorerHarness.Press(window, Key.Down, PhysicalKey.ArrowDown);
            ExplorerHarness.SelectedName(window).ShouldBe("src");
            ExplorerHarness.Press(window, Key.Right, PhysicalKey.ArrowRight);

            folder.ListedDirectories.ShouldBe(["", "src"]);
            ExplorerHarness.RenderedNames(window).ShouldBe(["docs", "src", "lib", "main.cs", "README.md"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Collapsing and expanding a directory again shows its kept children without listing it a second time.</summary>
    [Fact]
    public async Task CollapsingKeepsTheLoadedChildren()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var folder = ExplorerHarness.NewFolder();
            var tree = new WorkspaceTree();
            using var window = ExplorerHarness.Show(tree);
            tree.OpenFolder(folder);
            ShellHarness.Settle(window);

            ExplorerHarness.FocusRows(window);
            ExplorerHarness.Press(window, Key.Down, PhysicalKey.ArrowDown);
            ExplorerHarness.Press(window, Key.Right, PhysicalKey.ArrowRight);
            ExplorerHarness.Press(window, Key.Left, PhysicalKey.ArrowLeft);
            ExplorerHarness.RenderedNames(window).ShouldBe(RootRows);

            ExplorerHarness.Press(window, Key.Right, PhysicalKey.ArrowRight);

            ExplorerHarness.RenderedNames(window).ShouldBe(["docs", "src", "lib", "main.cs", "README.md"]);
            folder.ListedDirectories.Count(path => string.Equals(path, "src", StringComparison.Ordinal)).ShouldBe(1);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Down and Up move one visible row at a time and stop at the first and last rows.</summary>
    [Fact]
    public async Task DownAndUpMoveOneVisibleRowAndStopAtTheEnds()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = ShowOpenFolder(out _);
            ExplorerHarness.FocusRows(window);
            ExplorerHarness.SelectedName(window).ShouldBe("docs");

            ExplorerHarness.Press(window, Key.Up, PhysicalKey.ArrowUp);
            ExplorerHarness.SelectedName(window).ShouldBe("docs", "Up stops at the first row");

            ExplorerHarness.Press(window, Key.Down, PhysicalKey.ArrowDown);
            ExplorerHarness.SelectedName(window).ShouldBe("src");

            ExplorerHarness.Press(window, Key.Down, PhysicalKey.ArrowDown);
            ExplorerHarness.Press(window, Key.Down, PhysicalKey.ArrowDown);
            ExplorerHarness.SelectedName(window).ShouldBe("README.md", "Down stops at the last row");

            ExplorerHarness.Press(window, Key.Up, PhysicalKey.ArrowUp);
            ExplorerHarness.SelectedName(window).ShouldBe("src");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Home and End jump to the first and last visible rows.</summary>
    [Fact]
    public async Task HomeAndEndJumpToTheFirstAndLastVisibleRows()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = ShowOpenFolder(out _);
            ExplorerHarness.FocusRows(window);

            ExplorerHarness.Press(window, Key.End, PhysicalKey.End);
            ExplorerHarness.SelectedName(window).ShouldBe("README.md");

            ExplorerHarness.Press(window, Key.Home, PhysicalKey.Home);
            ExplorerHarness.SelectedName(window).ShouldBe("docs");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Right expands a collapsed directory, then moves into its first child; Left returns to the parent, then collapses.</summary>
    [Fact]
    public async Task RightAndLeftExpandCollapseAndMoveBetweenLevels()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = ShowOpenFolder(out _);
            ExplorerHarness.FocusRows(window);

            ExplorerHarness.Press(window, Key.Right, PhysicalKey.ArrowRight);
            ExplorerHarness.RenderedNames(window).ShouldBe(["docs", "readme.md", "src", "README.md"]);
            ExplorerHarness.SelectedName(window).ShouldBe("docs");

            ExplorerHarness.Press(window, Key.Right, PhysicalKey.ArrowRight);
            ExplorerHarness.SelectedName(window).ShouldBe("readme.md", "Right on an expanded directory enters its first child");

            ExplorerHarness.Press(window, Key.Left, PhysicalKey.ArrowLeft);
            ExplorerHarness.SelectedName(window).ShouldBe("docs", "Left on a file moves to its parent");

            ExplorerHarness.Press(window, Key.Left, PhysicalKey.ArrowLeft);
            ExplorerHarness.RenderedNames(window).ShouldBe(RootRows);
            ExplorerHarness.SelectedName(window).ShouldBe("docs", "Left on an expanded directory collapses it");

            ExplorerHarness.Press(window, Key.Left, PhysicalKey.ArrowLeft);
            ExplorerHarness.SelectedName(window).ShouldBe("docs", "the root level has no parent to move to");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter toggles a directory's expansion and leaves the selection on it.</summary>
    [Fact]
    public async Task EnterTogglesADirectory()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = ShowOpenFolder(out _);
            ExplorerHarness.FocusRows(window);

            ExplorerHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            ExplorerHarness.RenderedNames(window).ShouldBe(["docs", "readme.md", "src", "README.md"]);

            ExplorerHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            ExplorerHarness.RenderedNames(window).ShouldBe(RootRows);
            ExplorerHarness.SelectedName(window).ShouldBe("docs");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter on a file raises a request to open it, and the explorer expands nothing.</summary>
    [Fact]
    public async Task EnterOnAFileRaisesAnOpenRequest()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = ShowOpenFolder(out var view);
            var requested = new List<string>();
            view.FileOpenRequested += (_, args) => requested.Add(args.Entry.RelativePath);
            ExplorerHarness.FocusRows(window);
            ExplorerHarness.Press(window, Key.End, PhysicalKey.End);

            ExplorerHarness.Press(window, Key.Enter, PhysicalKey.Enter);

            requested.ShouldBe(["README.md"]);
            ExplorerHarness.RenderedNames(window).ShouldBe(RootRows);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The open-folder root is readable as a property: none before a folder opens, the folder's root path after.</summary>
    [Fact]
    public async Task TheOpenFolderRootIsReadableByTheOrchestrator()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var tree = new WorkspaceTree();
            using var window = ExplorerHarness.Show(tree);
            var view = ExplorerHarness.View(window);
            view.OpenFolderRoot.ShouldBeNull();

            tree.OpenFolder(ExplorerHarness.NewFolder());
            ShellHarness.Settle(window);

            view.OpenFolderRoot.ShouldBe(ExplorerHarness.Root);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A directory expanded by the orchestrator through the tree redraws the rows with its children.</summary>
    [Fact]
    public async Task AnExpansionMadeThroughTheTreeRedrawsTheRows()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var tree = new WorkspaceTree();
            using var window = ExplorerHarness.Show(tree);
            tree.OpenFolder(ExplorerHarness.NewFolder());
            ShellHarness.Settle(window);

            tree.ExpandPath("src/lib");
            ShellHarness.Settle(window);

            ExplorerHarness.RenderedNames(window).ShouldBe(["docs", "src", "lib", "util.cs", "main.cs", "README.md"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The root label, the row list and every row carry accessible names.</summary>
    [Fact]
    public async Task TheRootLabelTheListAndEveryRowHaveAccessibleNames()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = ShowOpenFolder(out var view);

            AutomationProperties.GetName(view.FindControl<TextBlock>("RootText")!).ShouldBe("Open folder");
            AutomationProperties.GetName(ExplorerHarness.Rows(window)).ShouldBe("Workspace files");
            ExplorerHarness.RenderedNames(window).ShouldAllBe(name => name.Length > 0);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The row list takes keyboard focus and Tab reaches it.</summary>
    [Fact]
    public async Task TheRowListIsAKeyboardTabStop()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = ShowOpenFolder(out _);

            ExplorerHarness.Rows(window).Focusable.ShouldBeTrue();
            ExplorerHarness.Rows(window).IsTabStop.ShouldBeTrue();
            ExplorerHarness.FocusRows(window);
            ExplorerHarness.Rows(window).IsFocused.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    // Shows the explorer on a tree that has the fixture folder open, and returns the window.
    private static ExplorerWindow ShowOpenFolder(out WorkspaceTreeView view)
    {
        var tree = new WorkspaceTree();
        var window = ExplorerHarness.Show(tree);
        tree.OpenFolder(ExplorerHarness.NewFolder());
        ShellHarness.Settle(window);
        view = ExplorerHarness.View(window);
        return window;
    }
}
