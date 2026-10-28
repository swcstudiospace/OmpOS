using Autonogrammer.Presentation.Editor;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>The explorer over an asynchronous file system (an ssh host): the same tree behaviour as the synchronous one, without blocking and without breaking it.</summary>
public sealed class AsyncWorkspaceTreeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static InMemoryWorkspaceFileSystem Tree() =>
        new InMemoryWorkspaceFileSystem("/home/dev/work").WithFile("README.md").WithFile("src/app/main.cs").WithFile("src/lib.cs");

    /// <summary>Opening lists the root once, sorts directories first and selects the first row.</summary>
    [Fact]
    public async Task OpenFolderAsync_ListsTheRootAndSelectsTheFirstRow()
    {
        var fileSystem = new YieldingAsyncWorkspaceFileSystem(Tree());
        var tree = new WorkspaceTree();

        await tree.OpenFolderAsync(fileSystem, Token).ConfigureAwait(true);

        tree.RootPath.ShouldBe("/home/dev/work");
        tree.Nodes.Select(node => node.Name).ShouldBe(["src", "README.md"]);
        tree.Selected.ShouldBeSameAs(tree.Nodes[0]);
        fileSystem.ListedDirectories.ShouldBe([string.Empty]);
    }

    /// <summary>A directory is listed on its first expansion and kept: collapsing and expanding again lists nothing.</summary>
    [Fact]
    public async Task ExpandAsync_ListsOnceAndKeepsTheChildren()
    {
        var fileSystem = new YieldingAsyncWorkspaceFileSystem(Tree());
        var tree = new WorkspaceTree();
        await tree.OpenFolderAsync(fileSystem, Token).ConfigureAwait(true);
        var src = tree.Nodes[0];

        await tree.ExpandAsync(src, Token).ConfigureAwait(true);
        tree.Collapse(src);
        await tree.ExpandAsync(src, Token).ConfigureAwait(true);

        src.IsExpanded.ShouldBeTrue();
        src.Children.Select(node => node.Name).ShouldBe(["app", "lib.cs"]);
        fileSystem.ListedDirectories.ShouldBe([string.Empty, "src"]);
    }

    /// <summary>A path is expanded level by level and an escaping path lists nothing.</summary>
    [Fact]
    public async Task ExpandPathAsync_WalksTheLevelsAndRefusesAnEscape()
    {
        var fileSystem = new YieldingAsyncWorkspaceFileSystem(Tree());
        var tree = new WorkspaceTree();
        await tree.OpenFolderAsync(fileSystem, Token).ConfigureAwait(true);

        var app = await tree.ExpandPathAsync("src/app", Token).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(() => tree.ExpandPathAsync("../outside", Token)).ConfigureAwait(true);

        app.RelativePath.ShouldBe("src/app");
        tree.VisibleNodes().Select(node => node.RelativePath).ShouldBe(["src", "src/app", "src/app/main.cs", "src/lib.cs", "README.md"]);
        fileSystem.ListedDirectories.ShouldBe([string.Empty, "src", "src/app"]);
    }

    /// <summary>Right and Enter on a directory that was never listed list it without blocking; Enter on a file returns it; the other keys behave as they do in the synchronous tree.</summary>
    [Fact]
    public async Task HandleKeyAsync_ListsAnUnloadedDirectoryThenAppliesTheKey()
    {
        var fileSystem = new YieldingAsyncWorkspaceFileSystem(Tree());
        var tree = new WorkspaceTree();
        await tree.OpenFolderAsync(fileSystem, Token).ConfigureAwait(true);

        (await tree.HandleKeyAsync(WorkspaceKey.Right, Token).ConfigureAwait(true)).ShouldBeNull();
        tree.Nodes[0].IsExpanded.ShouldBeTrue();
        (await tree.HandleKeyAsync(WorkspaceKey.Down, Token).ConfigureAwait(true)).ShouldBeNull();
        tree.Selected!.Name.ShouldBe("app");
        (await tree.HandleKeyAsync(WorkspaceKey.Enter, Token).ConfigureAwait(true)).ShouldBeNull();
        tree.Selected.IsExpanded.ShouldBeTrue();
        (await tree.HandleKeyAsync(WorkspaceKey.End, Token).ConfigureAwait(true)).ShouldBeNull();
        var opened = await tree.HandleKeyAsync(WorkspaceKey.Enter, Token).ConfigureAwait(true);

        opened.ShouldNotBeNull().RelativePath.ShouldBe("README.md");
        fileSystem.ListedDirectories.ShouldBe([string.Empty, "src", "src/app"]);
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => tree.HandleKeyAsync((WorkspaceKey)99, Token)).ConfigureAwait(true);
    }

    /// <summary>A directory that cannot be listed leaves the tree as it was and the failure reaches the caller; a cancelled listing too.</summary>
    [Fact]
    public async Task AFailingOrCancelledListingLeavesTheTreeUnchanged()
    {
        var fileSystem = new YieldingAsyncWorkspaceFileSystem(Tree());
        fileSystem.FailListing("src");
        var tree = new WorkspaceTree();
        await tree.OpenFolderAsync(fileSystem, Token).ConfigureAwait(true);
        var src = tree.Nodes[0];
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<IOException>(() => tree.ExpandAsync(src, Token)).ConfigureAwait(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tree.ExpandAsync(src, cancelled.Token)).ConfigureAwait(true);

        src.IsExpanded.ShouldBeFalse();
        src.IsLoaded.ShouldBeFalse();
        tree.VisibleNodes().Count.ShouldBe(2);
    }

    /// <summary>An entry that is not directly under its directory is refused before any node is made, as in the synchronous tree.</summary>
    [Fact]
    public async Task AnEntryOutsideItsDirectoryIsRefused()
    {
        var tree = new WorkspaceTree();

        await Should.ThrowAsync<InvalidOperationException>(() => tree.OpenFolderAsync(new RogueAsyncFileSystem(), Token)).ConfigureAwait(true);

        tree.Nodes.ShouldBeEmpty();
        tree.RootPath.ShouldBeNull();
    }

    /// <summary>The synchronous expansion of a tree that was opened asynchronously, and the other way round, say which method to use instead of listing on the caller's thread.</summary>
    [Fact]
    public async Task MixingTheSynchronousAndAsynchronousExpansionIsRefused()
    {
        var asyncTree = new WorkspaceTree();
        await asyncTree.OpenFolderAsync(new YieldingAsyncWorkspaceFileSystem(Tree()), Token).ConfigureAwait(true);
        var syncTree = new WorkspaceTree();
        syncTree.OpenFolder(Tree());

        var fromAsync = Should.Throw<InvalidOperationException>(() => asyncTree.Expand(asyncTree.Nodes[0]));
        var fromSync = await Should.ThrowAsync<InvalidOperationException>(() => syncTree.ExpandAsync(syncTree.Nodes[0], Token)).ConfigureAwait(true);

        fromAsync.Message.ShouldContain("ExpandAsync", Case.Sensitive);
        fromSync.Message.ShouldContain("Expand", Case.Sensitive);
    }

    /// <summary>Roots opened asynchronously fail one by one: the failing root shows its error, the others open, and a refresh brings the failed root back.</summary>
    [Fact]
    public async Task MultiRoot_OpenAsyncKeepsAFailureOnItsOwnRoot()
    {
        var answering = false;
        var explorer = new MultiRootWorkspaceTree();

        await explorer.OpenAsync(
        [
            new AsyncWorkspaceRootSource("web", "web", true, _ => Task.FromResult<IAsyncWorkspaceFileSystem>(new YieldingAsyncWorkspaceFileSystem(new InMemoryWorkspaceFileSystem("/srv/web").WithFile("index.html")))),
            new AsyncWorkspaceRootSource("api", "api", false, _ => answering
                ? Task.FromResult<IAsyncWorkspaceFileSystem>(new YieldingAsyncWorkspaceFileSystem(new InMemoryWorkspaceFileSystem("/srv/api").WithFile("a.cs")))
                : throw new IOException("The remote machine did not answer.")),
        ],
        Token).ConfigureAwait(true);

        explorer.FailedRoots.Select(failed => failed.Id).ToArray().ShouldBe(["api"]);
        explorer.FailedRoots[0].Error.ShouldNotBeNull().ShouldContain("did not answer", Case.Sensitive);
        explorer.Roots[0].Tree.Nodes.Select(node => node.Name).ShouldBe(["index.html"]);
        explorer.Roots[1].Tree.Nodes.ShouldBeEmpty();

        answering = true;
        await explorer.RefreshAsync("api", Token).ConfigureAwait(true);

        explorer.FailedRoots.ShouldBeEmpty();
        explorer.Roots[1].Tree.Nodes.Select(node => node.Name).ShouldBe(["a.cs"]);
        explorer.TryResolve("api/a.cs", out var root, out var relative).ShouldBeTrue();
        root!.Id.ShouldBe("api");
        relative.ShouldBe("a.cs");

        answering = false;
        await explorer.RefreshAsync("api", Token).ConfigureAwait(true);

        explorer.FailedRoots.ShouldHaveSingleItem().Id.ShouldBe("api");
        explorer.Roots[1].Tree.Nodes.Select(node => node.Name).ShouldBe(["a.cs"]);
    }

    /// <summary>Duplicate identifiers and names are refused before anything opens, and a synchronous source can be wrapped.</summary>
    [Fact]
    public async Task MultiRoot_OpenAsyncValidatesAndWrapsSynchronousSources()
    {
        var explorer = new MultiRootWorkspaceTree();
        var local = new WorkspaceRootSource("local", "Local", true, () => Tree());

        await Should.ThrowAsync<ArgumentException>(() => explorer.OpenAsync(
        [
            AsyncWorkspaceRootSource.FromSynchronous(local),
            new AsyncWorkspaceRootSource("other", "local", false, _ => Task.FromResult<IAsyncWorkspaceFileSystem>(new YieldingAsyncWorkspaceFileSystem(Tree()))),
        ],
        Token)).ConfigureAwait(true);
        explorer.Roots.ShouldBeEmpty();
        await explorer.OpenAsync([AsyncWorkspaceRootSource.FromSynchronous(local)], Token).ConfigureAwait(true);

        explorer.Roots.ShouldHaveSingleItem().Tree.Nodes.Select(node => node.Name).ShouldBe(["src", "README.md"]);
        Should.Throw<InvalidOperationException>(() => explorer.Refresh("local"));
    }

    private sealed class RogueAsyncFileSystem : IAsyncWorkspaceFileSystem
    {
        public string RootPath => "/rogue";

        public Task<IReadOnlyList<WorkspaceEntry>> ListDirectoryAsync(string relativeDirectory, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkspaceEntry>>([new WorkspaceEntry("passwd", "../etc/passwd", IsDirectory: false)]);
    }
}
