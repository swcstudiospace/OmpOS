using Autonogrammer.Presentation.Editor;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>The adapter that presents a synchronous workspace file system through the asynchronous interface: the same behaviour, the same exceptions, a size cap on reads.</summary>
public sealed class AsyncWorkspaceFileSystemAdapterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "autonogrammer-adapter-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates a small workspace on disk.</summary>
    public AsyncWorkspaceFileSystemAdapterTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "a.txt"), "alpha");
        File.WriteAllText(Path.Combine(_root, "sub", "b.txt"), "beta");
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Removes the workspace.</summary>
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private AsyncWorkspaceFileSystemAdapter Adapter() => new(new LocalWorkspaceFileSystem(_root));

    /// <summary>List, read, write, create and delete behave like the wrapped file system, and the stale guard still applies.</summary>
    [Fact]
    public async Task EveryOperationDelegatesToTheWrappedFileSystem()
    {
        var adapter = Adapter();

        (await adapter.ListDirectoryAsync("sub", Token).ConfigureAwait(true)).ShouldHaveSingleItem().RelativePath.ShouldBe("sub/b.txt");
        (await adapter.ReadFileAsync("a.txt", WorkspaceFileLimits.DefaultMaxReadBytes, Token).ConfigureAwait(true)).ShouldBe("alpha");
        await adapter.WriteFileAsync("a.txt", "alpha two", "alpha", Token).ConfigureAwait(true);
        await adapter.CreateFileAsync("new/c.txt", "gamma", Token).ConfigureAwait(true);
        await Should.ThrowAsync<StaleWorkspaceFileException>(() => adapter.DeleteFileAsync("a.txt", "alpha", Token)).ConfigureAwait(true);
        await adapter.DeleteFileAsync("a.txt", "alpha two", Token).ConfigureAwait(true);

        File.Exists(Path.Combine(_root, "a.txt")).ShouldBeFalse();
        (await File.ReadAllTextAsync(Path.Combine(_root, "new", "c.txt"), Token).ConfigureAwait(true)).ShouldBe("gamma");
        adapter.RootPath.ShouldBe(Path.GetFullPath(_root));
    }

    /// <summary>A read larger than the cap is refused with the dedicated exception, counted in UTF-8 bytes.</summary>
    [Fact]
    public async Task AReadLargerThanTheCapIsRefused()
    {
        var adapter = Adapter();
        await File.WriteAllTextAsync(Path.Combine(_root, "wide.txt"), "ééééé", TestContext.Current.CancellationToken).ConfigureAwait(true);

        await Should.ThrowAsync<WorkspaceFileTooLargeException>(() => adapter.ReadFileAsync("wide.txt", 9, Token)).ConfigureAwait(true);
        (await adapter.ReadFileAsync("wide.txt", 10, Token).ConfigureAwait(true)).ShouldBe("ééééé");
    }

    /// <summary>Stat finds files and directories through the listing of their parent and returns null for a missing path.</summary>
    [Fact]
    public async Task StatUsesTheParentListing()
    {
        var adapter = Adapter();

        (await adapter.StatAsync("sub", Token).ConfigureAwait(true)).ShouldNotBeNull().IsDirectory.ShouldBeTrue();
        (await adapter.StatAsync("sub/b.txt", Token).ConfigureAwait(true)).ShouldNotBeNull().IsDirectory.ShouldBeFalse();
        (await adapter.StatAsync(string.Empty, Token).ConfigureAwait(true)).ShouldNotBeNull().IsDirectory.ShouldBeTrue();
        (await adapter.StatAsync("nope/x.txt", Token).ConfigureAwait(true)).ShouldBeNull();
        (await adapter.StatAsync("sub/none.txt", Token).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>A read-only wrapped file system stays read-only through the adapter.</summary>
    [Fact]
    public async Task AReadOnlyFileSystemStaysReadOnly()
    {
        var adapter = new AsyncWorkspaceFileSystemAdapter(new InMemoryWorkspaceFileSystem().WithFile("x.txt", "x"));

        await Should.ThrowAsync<NotSupportedException>(() => adapter.CreateFileAsync("y.txt", "y", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<NotSupportedException>(() => adapter.DeleteFileAsync("x.txt", "x", Token)).ConfigureAwait(true);
    }

    /// <summary>An implementation of the interface that overrides nothing but listing is read-only: every other member says so.</summary>
    [Fact]
    public async Task ADefaultImplementationIsReadOnly()
    {
        IAsyncWorkspaceFileSystem listingOnly = new ListingOnly();

        await Should.ThrowAsync<NotSupportedException>(() => listingOnly.ReadFileAsync("a", 10, Token)).ConfigureAwait(true);
        await Should.ThrowAsync<NotSupportedException>(() => listingOnly.WriteFileAsync("a", "b", "c", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<NotSupportedException>(() => listingOnly.CreateFileAsync("a", "b", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<NotSupportedException>(() => listingOnly.DeleteFileAsync("a", "b", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<NotSupportedException>(() => listingOnly.StatAsync("a", Token)).ConfigureAwait(true);
    }

    private sealed class ListingOnly : IAsyncWorkspaceFileSystem
    {
        public string RootPath => "/x";

        public Task<IReadOnlyList<WorkspaceEntry>> ListDirectoryAsync(string relativeDirectory, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkspaceEntry>>([]);
    }
}
