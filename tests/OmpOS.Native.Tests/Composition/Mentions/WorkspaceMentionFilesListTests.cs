using OmpOS.Native.Composition.Mentions;
using OmpOS.Native.Tests.AgentLoop;
using OmpOS.Presentation.Mentions;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Composition.Mentions;

/// <summary>The tree of a local workspace as the file and folder mentions see it: what is listed, what is left out, and when the tree is walked again.</summary>
public sealed class WorkspaceMentionFilesListTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<List<string>> PathsAsync(WorkspaceMentionFiles files) =>
        [.. (await files.ListAsync(Token).ConfigureAwait(true)).Select(static entry => entry.Path)];

    /// <summary>Ignore files and the global list hide entries, an ignored folder is not entered, and the folders no one wants (<c>node_modules</c>, <c>.git</c>, <c>bin</c>, <c>obj</c>) never appear.</summary>
    [Fact]
    public async Task IgnoreFilesTheGlobalListAndTheSkippedFoldersLeaveEntriesOut()
    {
        using var tree = new MentionWorkspaceTree();
        tree.Write(".gitignore", "*.log\nbuild/\n");
        tree.Write("README.md", "readme");
        tree.Write("src/a.cs", "a");
        tree.Write("src/debug.log", "log");
        tree.Write("build/out.txt", "out");
        tree.Write("docs/secret.txt", "secret");
        tree.Write("docs/guide.md", "guide");
        tree.Write("node_modules/pkg/index.js", "js");
        tree.Write("bin/tool.txt", "bin");
        tree.Write("obj/cache.txt", "obj");
        tree.Write(".git/config", "[core]");
        var matcher = IgnoreMatcherFactory.Create(["secret.txt"]);
        var files = new WorkspaceMentionFiles(tree.Root, matcher);

        var paths = await PathsAsync(files).ConfigureAwait(true);

        paths.ShouldBe([".gitignore", "README.md", "docs", "docs/guide.md", "src", "src/a.cs"], ignoreOrder: true);
    }

    /// <summary>A folder's own ignore file is read only when the hierarchy is on; the root's ignore file is read either way.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task NestedIgnoreFilesAreReadOnlyForAHierarchicalWorkspace(bool hierarchical, bool nestedFileListed)
    {
        using var tree = new MentionWorkspaceTree();
        tree.Write(".cursorignore", "root-private.txt\n");
        tree.Write("root-private.txt", "x");
        tree.Write("root-public.txt", "x");
        tree.Write("pkg/.cursorignore", "private.txt\n");
        tree.Write("pkg/private.txt", "x");
        tree.Write("pkg/public.txt", "x");
        var files = new WorkspaceMentionFiles(tree.Root, IgnoreMatcherFactory.Create([]), hierarchical);

        var paths = await PathsAsync(files).ConfigureAwait(true);

        paths.Contains("root-private.txt").ShouldBeFalse();
        paths.Contains("root-public.txt").ShouldBeTrue();
        paths.Contains("pkg/public.txt").ShouldBeTrue();
        paths.Contains("pkg/private.txt").ShouldBe(nestedFileListed);
    }

    /// <summary>With no matcher nothing is ignored, but the skipped folders still are.</summary>
    [Fact]
    public async Task WithoutAMatcherOnlyTheSkippedFoldersAreLeftOut()
    {
        using var tree = new MentionWorkspaceTree();
        tree.Write(".gitignore", "*.log\n");
        tree.Write("a.log", "x");
        tree.Write("node_modules/x.js", "x");
        var files = new WorkspaceMentionFiles(tree.Root);

        var paths = await PathsAsync(files).ConfigureAwait(true);

        paths.ShouldBe([".gitignore", "a.log"], ignoreOrder: true);
    }

    /// <summary>Entries use forward slashes, folders have size 0 and files carry their byte size.</summary>
    [Fact]
    public async Task EntriesCarryRelativePathsFoldersAndSizes()
    {
        using var tree = new MentionWorkspaceTree();
        tree.Write("a/b/c.txt", "hello");
        var files = new WorkspaceMentionFiles(tree.Root);

        var entries = await files.ListAsync(Token).ConfigureAwait(true);

        entries.ShouldBe(
            [new MentionFileEntry("a", true, 0), new MentionFileEntry("a/b", true, 0), new MentionFileEntry("a/b/c.txt", false, 5)],
            ignoreOrder: true);
    }

    /// <summary>The listing stops at the cap and the walk is breadth first, so shallow files are kept before deep ones.</summary>
    [Fact]
    public async Task TheListingStopsAtTheCapAndKeepsShallowEntriesFirst()
    {
        using var tree = new MentionWorkspaceTree();
        for (var index = 0; index < 6; index++)
        {
            tree.Write("top" + index + ".txt", "x");
        }

        for (var index = 0; index < 6; index++)
        {
            tree.Write("deep/inner" + index + ".txt", "x");
        }

        var files = new WorkspaceMentionFiles(tree.Root, maxEntries: 7);

        var paths = await PathsAsync(files).ConfigureAwait(true);

        paths.Count.ShouldBe(7);
        paths.Count(static path => path.StartsWith("top", StringComparison.Ordinal)).ShouldBe(6);
        paths.ShouldContain("deep", Case.Sensitive);
    }

    /// <summary>The tree is walked once per cache lifetime: a file added meanwhile shows up only after the clock passes it.</summary>
    [Fact]
    public async Task TheListingIsKeptUntilTheCacheLifetimeEnds()
    {
        using var tree = new MentionWorkspaceTree();
        tree.Write("first.txt", "x");
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero));
        var files = new WorkspaceMentionFiles(tree.Root, timeProvider: clock);

        var before = await PathsAsync(files).ConfigureAwait(true);
        tree.Write("second.txt", "x");
        var cached = await PathsAsync(files).ConfigureAwait(true);
        clock.Advance(WorkspaceMentionFiles.CacheLifetime);
        var refreshed = await PathsAsync(files).ConfigureAwait(true);

        before.ShouldBe(["first.txt"], ignoreOrder: true);
        cached.ShouldBe(["first.txt"], ignoreOrder: true);
        refreshed.ShouldBe(["first.txt", "second.txt"], ignoreOrder: true);
    }

    /// <summary>A symbolic link, to a file or to a folder outside the workspace, is neither listed nor entered.</summary>
    [Fact]
    public async Task SymbolicLinksAreNeitherListedNorFollowed()
    {
        using var tree = new MentionWorkspaceTree();
        tree.Write("real.txt", "x");
        tree.WriteOutside("secret.txt", "secret");
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(tree.Root, "linked-folder"), tree.Outside);
            File.CreateSymbolicLink(Path.Combine(tree.Root, "linked-file.txt"), Path.Combine(tree.Outside, "secret.txt"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("This account cannot create symbolic links.");
        }

        var files = new WorkspaceMentionFiles(tree.Root);

        var paths = await PathsAsync(files).ConfigureAwait(true);

        paths.ShouldBe(["real.txt"], ignoreOrder: true);
    }

    /// <summary>A workspace folder that does not exist lists nothing and does not throw.</summary>
    [Fact]
    public async Task AMissingRootListsNothing()
    {
        using var tree = new MentionWorkspaceTree();
        var files = new WorkspaceMentionFiles(Path.Combine(tree.Base, "absent"));

        var paths = await PathsAsync(files).ConfigureAwait(true);

        paths.ShouldBeEmpty();
    }

    /// <summary>A cancelled token stops the call before any walk starts.</summary>
    [Fact]
    public async Task ACancelledTokenStopsTheListing()
    {
        using var tree = new MentionWorkspaceTree();
        var files = new WorkspaceMentionFiles(tree.Root);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(() => files.ListAsync(cancelled.Token)).ConfigureAwait(true);
    }
}
