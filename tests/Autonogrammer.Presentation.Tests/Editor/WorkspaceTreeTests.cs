using Autonogrammer.Presentation.Editor;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>The workspace file tree: lazy expansion, root containment and the keyboard model (ADE-10).</summary>
public sealed class WorkspaceTreeTests
{
    /// <summary>Opening a folder lists only the root: directories first, then files, and the first row is selected.</summary>
    [Fact]
    public void OpenFolder_ListsOnlyTheRootAndSelectsTheFirstRow()
    {
        var fileSystem = Fixture();
        var tree = new WorkspaceTree();

        tree.OpenFolder(fileSystem);

        fileSystem.ListedDirectories.ShouldBe([string.Empty]);
        tree.RootPath.ShouldBe("/workspace");
        tree.Nodes.Select(node => node.Name).ShouldBe(["docs", "src", "README.md"]);
        tree.Selected.ShouldNotBeNull().Name.ShouldBe("docs");
    }

    /// <summary>A directory's children load on its first expansion, are kept, and are not listed again on re-expansion.</summary>
    [Fact]
    public void Expand_ListsChildrenOnceOnFirstExpansionAndKeepsThem()
    {
        var fileSystem = Fixture();
        var tree = new WorkspaceTree();
        tree.OpenFolder(fileSystem);
        var src = Find(tree, "src");

        src.IsLoaded.ShouldBeFalse();
        src.Children.ShouldBeEmpty();

        tree.Expand(src);
        tree.Collapse(src);
        tree.Expand(src);

        src.IsExpanded.ShouldBeTrue();
        src.Children.Select(node => node.Name).ShouldBe(["Models", "Program.cs"]);
        fileSystem.ListedDirectories.ShouldBe([string.Empty, "src"]);
    }

    /// <summary>Collapsing a directory that holds the selection moves the selection onto the directory.</summary>
    [Fact]
    public void Collapse_MovesTheSelectionOntoTheDirectory()
    {
        var tree = new WorkspaceTree();
        tree.OpenFolder(Fixture());
        var src = Find(tree, "src");
        tree.Expand(src);
        tree.Select(Find(tree, "src/Program.cs"));

        tree.Collapse(src);

        tree.Selected.ShouldBeSameAs(src);
        tree.VisibleNodes().Select(node => node.RelativePath).ShouldBe(["docs", "src", "README.md"]);
    }

    /// <summary>ExpandPath loads each level on the way to the directory and returns it.</summary>
    [Fact]
    public void ExpandPath_LoadsEachLevelAndReturnsTheDirectory()
    {
        var fileSystem = Fixture();
        var tree = new WorkspaceTree();
        tree.OpenFolder(fileSystem);

        var models = tree.ExpandPath("src/Models");

        models.Name.ShouldBe("Models");
        models.Depth.ShouldBe(1);
        fileSystem.ListedDirectories.ShouldBe([string.Empty, "src", "src/Models"]);
        tree.VisibleNodes().Select(node => node.Name).ShouldBe(["docs", "src", "Models", "Item.cs", "Program.cs", "README.md"]);
    }

    /// <summary>A path that escapes the root is refused before anything is listed, so no directory outside the root is read.</summary>
    [Fact]
    public void ExpandPath_RefusesAnEscapingPathBeforeListingAnything()
    {
        var fileSystem = Fixture();
        var tree = new WorkspaceTree();
        tree.OpenFolder(fileSystem);

        Should.Throw<ArgumentException>(() => tree.ExpandPath("../outside"));
        Should.Throw<ArgumentException>(() => tree.ExpandPath("src/../../outside"));

        fileSystem.ListedDirectories.ShouldBe([string.Empty]);
    }

    /// <summary>An empty path, a file, and a missing directory are all refused.</summary>
    [Fact]
    public void ExpandPath_RefusesTheRootFilesAndMissingDirectories()
    {
        var tree = new WorkspaceTree();
        tree.OpenFolder(Fixture());

        Should.Throw<ArgumentException>(() => tree.ExpandPath(string.Empty));
        Should.Throw<ArgumentException>(() => tree.ExpandPath("README.md"));
        Should.Throw<ArgumentException>(() => tree.ExpandPath("nowhere"));
    }

    /// <summary>A file system that reports an entry outside its directory is refused; the tree keeps no node from it.</summary>
    [Fact]
    public void OpenFolder_RefusesAnEntryOutsideTheRoot_AndTheTreeIsUnchanged()
    {
        var rogue = new RogueWorkspaceFileSystem(_ => [new WorkspaceEntry("passwd", "../passwd", IsDirectory: false)]);
        var tree = new WorkspaceTree();

        Should.Throw<InvalidOperationException>(() => tree.OpenFolder(rogue));

        tree.Nodes.ShouldBeEmpty();
        tree.RootPath.ShouldBeNull();
    }

    /// <summary>An entry listed under a directory must be that directory's child; a sibling path is refused and the directory stays collapsed.</summary>
    [Fact]
    public void Expand_RefusesAChildThatIsNotUnderItsDirectory()
    {
        var rogue = new RogueWorkspaceFileSystem(directory => directory.Length == 0
            ? [new WorkspaceEntry("src", "src", IsDirectory: true)]
            : [new WorkspaceEntry("x", "elsewhere/x", IsDirectory: false)]);
        var tree = new WorkspaceTree();
        tree.OpenFolder(rogue);
        var src = Find(tree, "src");

        Should.Throw<InvalidOperationException>(() => tree.Expand(src));

        src.IsExpanded.ShouldBeFalse();
        src.IsLoaded.ShouldBeFalse();
    }

    /// <summary>Down and Up move over the visible rows and stop at the first and last.</summary>
    [Fact]
    public void Keyboard_DownAndUpMoveOverVisibleRowsAndStopAtTheEnds()
    {
        var tree = new WorkspaceTree();
        tree.OpenFolder(Fixture());

        tree.HandleKey(WorkspaceKey.Down);
        tree.Selected.ShouldNotBeNull().RelativePath.ShouldBe("src");
        tree.HandleKey(WorkspaceKey.Down);
        tree.Selected.ShouldNotBeNull().RelativePath.ShouldBe("README.md");
        tree.HandleKey(WorkspaceKey.Down);
        tree.Selected.ShouldNotBeNull().RelativePath.ShouldBe("README.md");
        tree.HandleKey(WorkspaceKey.Up);
        tree.Selected.ShouldNotBeNull().RelativePath.ShouldBe("src");
        tree.HandleKey(WorkspaceKey.Up);
        tree.HandleKey(WorkspaceKey.Up);
        tree.Selected.ShouldNotBeNull().RelativePath.ShouldBe("docs");
    }

    /// <summary>Right expands a collapsed directory, and on an expanded one it moves into the first child.</summary>
    [Fact]
    public void Keyboard_RightExpandsThenEntersTheDirectory()
    {
        var tree = new WorkspaceTree();
        tree.OpenFolder(Fixture());

        tree.HandleKey(WorkspaceKey.Right);
        tree.Selected.ShouldNotBeNull().RelativePath.ShouldBe("docs");
        Find(tree, "docs").IsExpanded.ShouldBeTrue();

        tree.HandleKey(WorkspaceKey.Right);
        tree.Selected.ShouldNotBeNull().RelativePath.ShouldBe("docs/guide.md");
    }

    /// <summary>Left collapses an expanded directory, and otherwise moves to the parent; at the top level it stays put.</summary>
    [Fact]
    public void Keyboard_LeftCollapsesThenMovesToTheParent()
    {
        var tree = new WorkspaceTree();
        tree.OpenFolder(Fixture());
        tree.ExpandPath("docs");
        tree.Select(Find(tree, "docs/guide.md"));

        tree.HandleKey(WorkspaceKey.Left);
        tree.Selected.ShouldNotBeNull().RelativePath.ShouldBe("docs");

        tree.HandleKey(WorkspaceKey.Left);
        Find(tree, "docs").IsExpanded.ShouldBeFalse();
        tree.Selected.ShouldNotBeNull().RelativePath.ShouldBe("docs");

        tree.HandleKey(WorkspaceKey.Left);
        tree.Selected.ShouldNotBeNull().RelativePath.ShouldBe("docs");
    }

    /// <summary>Enter returns the file to open, and on a directory it toggles the expansion.</summary>
    [Fact]
    public void Keyboard_EnterOpensAFileAndTogglesADirectory()
    {
        var tree = new WorkspaceTree();
        tree.OpenFolder(Fixture());
        tree.Select(Find(tree, "README.md"));

        var opened = tree.HandleKey(WorkspaceKey.Enter);
        opened.ShouldNotBeNull().RelativePath.ShouldBe("README.md");
        opened.IsDirectory.ShouldBeFalse();

        tree.Select(Find(tree, "docs"));
        tree.HandleKey(WorkspaceKey.Enter).ShouldBeNull();
        Find(tree, "docs").IsExpanded.ShouldBeTrue();
        tree.HandleKey(WorkspaceKey.Enter).ShouldBeNull();
        Find(tree, "docs").IsExpanded.ShouldBeFalse();
    }

    /// <summary>Home and End select the first and last visible rows.</summary>
    [Fact]
    public void Keyboard_HomeAndEndSelectTheFirstAndLastRows()
    {
        var tree = new WorkspaceTree();
        tree.OpenFolder(Fixture());

        tree.HandleKey(WorkspaceKey.End);
        tree.Selected.ShouldNotBeNull().RelativePath.ShouldBe("README.md");

        tree.HandleKey(WorkspaceKey.Home);
        tree.Selected.ShouldNotBeNull().RelativePath.ShouldBe("docs");
    }

    /// <summary>A row that is hidden inside a collapsed directory cannot be selected.</summary>
    [Fact]
    public void Select_RefusesARowHiddenInsideACollapsedDirectory()
    {
        var tree = new WorkspaceTree();
        tree.OpenFolder(Fixture());
        var guide = tree.ExpandPath("docs").Children[0];
        tree.Collapse(Find(tree, "docs"));

        Should.Throw<ArgumentException>(() => tree.Select(guide));
    }

    private static InMemoryWorkspaceFileSystem Fixture() =>
        new InMemoryWorkspaceFileSystem()
            .WithFile("README.md")
            .WithFile("docs/guide.md")
            .WithFile("src/Program.cs")
            .WithFile("src/Models/Item.cs");

    private static WorkspaceNode Find(WorkspaceTree tree, string relativePath) =>
        tree.VisibleNodes().Single(node => string.Equals(node.RelativePath, relativePath, StringComparison.Ordinal));

    /// <summary>A file system that answers each directory with the given entries, for exercising the tree's checks.</summary>
    private sealed class RogueWorkspaceFileSystem(Func<string, IReadOnlyList<WorkspaceEntry>> listing) : IWorkspaceFileSystem
    {
        public string RootPath => "/rogue";

        public IReadOnlyList<WorkspaceEntry> ListDirectory(string relativeDirectory) => listing(relativeDirectory);
    }
}
