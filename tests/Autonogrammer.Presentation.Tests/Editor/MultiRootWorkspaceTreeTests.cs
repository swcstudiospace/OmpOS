using Autonogrammer.Presentation.Editor;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>The multi-root explorer: one tree per root under the root name, per-root errors, and root-qualified paths.</summary>
public sealed class MultiRootWorkspaceTreeTests
{
    private static WorkspaceRootSource Source(string id, string name, IWorkspaceFileSystem fileSystem, bool primary = false) =>
        new(id, name, primary, () => fileSystem);

    private static InMemoryWorkspaceFileSystem Web() =>
        new InMemoryWorkspaceFileSystem("/repos/web").WithFile("package.json").WithFile("src/index.ts");

    private static InMemoryWorkspaceFileSystem Api() =>
        new InMemoryWorkspaceFileSystem("/repos/api").WithFile("api.csproj").WithFile("Controllers/HomeController.cs");

    /// <summary>Each root lists only its own top level when opened, and the roots keep the order they were given.</summary>
    [Fact]
    public void Open_ListsTheTopLevelOfEveryRootIndependently()
    {
        var web = Web();
        var api = Api();
        var explorer = new MultiRootWorkspaceTree();

        explorer.Open([Source("web", "web", web, primary: true), Source("api", "api", api)]);

        explorer.Roots.Select(root => root.DisplayName).ShouldBe(["web", "api"]);
        explorer.Roots[0].IsPrimary.ShouldBeTrue();
        explorer.Roots[1].IsPrimary.ShouldBeFalse();
        explorer.Roots[0].Tree.RootPath.ShouldBe("/repos/web");
        explorer.Roots[0].Tree.Nodes.Select(node => node.Name).ShouldBe(["src", "package.json"]);
        explorer.Roots[1].Tree.Nodes.Select(node => node.Name).ShouldBe(["Controllers", "api.csproj"]);
        web.ListedDirectories.ShouldBe([string.Empty]);
        api.ListedDirectories.ShouldBe([string.Empty]);
        explorer.SelectedRoot.ShouldBeSameAs(explorer.Roots[0]);
        explorer.FailedRoots.ShouldBeEmpty();
    }

    /// <summary>Expanding a directory in one root lists only that root, and collapsing keeps the other root's state.</summary>
    [Fact]
    public void Expand_InOneRootDoesNotTouchTheOther()
    {
        var web = Web();
        var api = Api();
        var explorer = new MultiRootWorkspaceTree();
        explorer.Open([Source("web", "web", web, primary: true), Source("api", "api", api)]);

        var src = explorer.Roots[0].Tree.ExpandPath("src");

        src.IsExpanded.ShouldBeTrue();
        web.ListedDirectories.ShouldBe([string.Empty, "src"]);
        api.ListedDirectories.ShouldBe([string.Empty]);
        explorer.Roots[1].Tree.VisibleNodes().Select(node => node.Name).ShouldBe(["Controllers", "api.csproj"]);
    }

    /// <summary>A root whose folder is missing shows its error and an empty tree while the others open normally.</summary>
    [Fact]
    public void Open_RecordsAMissingFolderOnItsOwnRootOnly()
    {
        var explorer = new MultiRootWorkspaceTree();

        explorer.Open(
        [
            Source("web", "web", Web(), primary: true),
            new WorkspaceRootSource("gone", "gone", false, () => throw new DirectoryNotFoundException("The workspace folder '/repos/gone' does not exist.")),
            Source("api", "api", Api()),
        ]);

        var gone = explorer.Roots[1];
        gone.HasError.ShouldBeTrue();
        gone.Error.ShouldNotBeNull().ShouldContain("gone", Case.Sensitive);
        gone.Error.ShouldContain("does not exist", Case.Sensitive);
        gone.Tree.Nodes.ShouldBeEmpty();
        explorer.Roots[0].HasError.ShouldBeFalse();
        explorer.Roots[0].Tree.Nodes.Count.ShouldBe(2);
        explorer.Roots[2].Tree.Nodes.Count.ShouldBe(2);
        explorer.FailedRoots.ShouldBe([gone]);
    }

    /// <summary>A file system that opens but cannot list its root, through a link that leaves it, fails only that root.</summary>
    [Fact]
    public void Open_RecordsAListingFailureOnItsOwnRoot()
    {
        var explorer = new MultiRootWorkspaceTree();

        explorer.Open(
        [
            new WorkspaceRootSource("locked", "locked", true, () => new ThrowingFileSystem()),
            Source("api", "api", Api()),
        ]);

        explorer.Roots[0].Error.ShouldNotBeNull().ShouldContain("outside the workspace root", Case.Sensitive);
        explorer.Roots[1].HasError.ShouldBeFalse();
    }

    /// <summary>Refreshing one root opens it again once its folder is back, without listing the other roots again.</summary>
    [Fact]
    public void Refresh_ReopensOnlyTheNamedRoot()
    {
        var web = Web();
        var folderBack = false;
        var explorer = new MultiRootWorkspaceTree();
        explorer.Open(
        [
            Source("web", "web", web, primary: true),
            new WorkspaceRootSource("api", "api", false, () => folderBack ? Api() : throw new DirectoryNotFoundException("missing")),
        ]);
        explorer.Roots[1].HasError.ShouldBeTrue();

        folderBack = true;
        explorer.Refresh("api");

        explorer.Roots[1].HasError.ShouldBeFalse();
        explorer.Roots[1].Tree.Nodes.Count.ShouldBe(2);
        explorer.FailedRoots.ShouldBeEmpty();
        web.ListedDirectories.ShouldBe([string.Empty]);
        Should.Throw<ArgumentException>(() => explorer.Refresh("nope"));
    }

    /// <summary>Two roots with the same identifier or the same name ignoring case are refused and the explorer is unchanged.</summary>
    [Fact]
    public void Open_RefusesRepeatedIdentifiersAndNamesWithoutChangingTheExplorer()
    {
        var explorer = new MultiRootWorkspaceTree();
        explorer.Open([Source("web", "web", Web(), primary: true)]);

        Should.Throw<ArgumentException>(() => explorer.Open([Source("a", "Web", Web()), Source("b", "web", Api())]));
        Should.Throw<ArgumentException>(() => explorer.Open([Source("a", "one", Web()), Source("a", "two", Api())]));
        Should.Throw<ArgumentException>(() => explorer.Open([Source("a", " ", Web())]));

        explorer.Roots.Select(root => root.Id).ShouldBe(["web"]);
    }

    /// <summary>Opening again replaces the roots and selects the first.</summary>
    [Fact]
    public void Open_ReplacesThePreviousRoots()
    {
        var explorer = new MultiRootWorkspaceTree();
        explorer.Open([Source("web", "web", Web(), primary: true), Source("api", "api", Api())]);
        explorer.SelectRoot("api");
        explorer.SelectedRoot.ShouldBeSameAs(explorer.Roots[1]);

        explorer.Open([Source("docs", "docs", new InMemoryWorkspaceFileSystem("/repos/docs").WithFile("index.md"), primary: true)]);

        explorer.Roots.Select(root => root.Id).ShouldBe(["docs"]);
        explorer.SelectedRoot.ShouldBeSameAs(explorer.Roots[0]);
    }

    /// <summary>Qualified paths put the root name first; the root itself is just its name.</summary>
    [Fact]
    public void Qualify_PutsTheRootNameInFrontOfTheRelativePath()
    {
        var explorer = new MultiRootWorkspaceTree();
        explorer.Open([Source("web", "web", Web(), primary: true), Source("api", "api", Api())]);

        explorer.Qualify(explorer.Roots[0], "src/index.ts").ShouldBe("web/src/index.ts");
        explorer.Qualify(explorer.Roots[1], string.Empty).ShouldBe("api");
        explorer.Qualify(explorer.Roots[1], "./Controllers/../api.csproj").ShouldBe("api/api.csproj");
        Should.Throw<ArgumentException>(() => explorer.Qualify(explorer.Roots[0], "../escape"));
    }

    /// <summary>Resolving splits the leading root name off, prefers the longest matching name, and refuses a path that escapes its root.</summary>
    [Fact]
    public void TryResolve_SplitsTheRootNameAndRefusesEscapes()
    {
        var explorer = new MultiRootWorkspaceTree();
        explorer.Open([Source("web", "web", Web(), primary: true), Source("webapp", "web/app", Api())]);

        explorer.TryResolve("web/src/index.ts", out var root, out var relative).ShouldBeTrue();
        root.ShouldBeSameAs(explorer.Roots[0]);
        relative.ShouldBe("src/index.ts");

        explorer.TryResolve("web/app/Controllers", out root, out relative).ShouldBeTrue();
        root.ShouldBeSameAs(explorer.Roots[1]);
        relative.ShouldBe("Controllers");

        explorer.TryResolve("web", out root, out relative).ShouldBeTrue();
        root.ShouldBeSameAs(explorer.Roots[0]);
        relative.ShouldBeEmpty();

        explorer.TryResolve("web/../web/app/x", out _, out _).ShouldBeFalse();
        explorer.TryResolve("website/index.ts", out _, out _).ShouldBeFalse();
        explorer.TryResolve("unknown/x", out _, out _).ShouldBeFalse();
    }

    /// <summary>The explorer works over real folders: each root lists its own directory and a missing folder fails alone.</summary>
    [Fact]
    public void Open_WorksOverRealFolders()
    {
        var parent = Path.Combine(Path.GetTempPath(), "autonogrammer-multiroot-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(parent, "web", "src"));
            File.WriteAllText(Path.Combine(parent, "web", "README.md"), "web");
            Directory.CreateDirectory(Path.Combine(parent, "api"));
            File.WriteAllText(Path.Combine(parent, "api", "Program.cs"), "api");
            var missing = Path.Combine(parent, "missing");
            var explorer = new MultiRootWorkspaceTree();

            explorer.Open(
            [
                new WorkspaceRootSource("web", "web", true, () => new LocalWorkspaceFileSystem(Path.Combine(parent, "web"))),
                new WorkspaceRootSource("api", "api", false, () => new LocalWorkspaceFileSystem(Path.Combine(parent, "api"))),
                new WorkspaceRootSource("missing", "missing", false, () => new LocalWorkspaceFileSystem(missing)),
            ]);

            explorer.Roots[0].Tree.Nodes.Select(node => node.Name).ShouldBe(["src", "README.md"]);
            explorer.Roots[1].Tree.Nodes.Select(node => node.Name).ShouldBe(["Program.cs"]);
            explorer.Roots[2].HasError.ShouldBeTrue();
            explorer.FailedRoots.Count.ShouldBe(1);
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
    }

    private sealed class ThrowingFileSystem : IWorkspaceFileSystem
    {
        public string RootPath => "/repos/locked";

        public IReadOnlyList<WorkspaceEntry> ListDirectory(string relativeDirectory) =>
            throw new UnauthorizedAccessException("The path resolves outside the workspace root.");
    }
}
