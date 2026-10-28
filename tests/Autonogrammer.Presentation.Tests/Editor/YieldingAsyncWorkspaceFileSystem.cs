using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Presentation.Tests;

/// <summary>
/// An asynchronous workspace file system over an <see cref="InMemoryWorkspaceFileSystem"/>: every call yields before it answers, honours its cancellation token
/// and can be told to fail a directory, so tests of the asynchronous explorer prove that nothing blocks and that a failure stays on its own root.
/// </summary>
internal sealed class YieldingAsyncWorkspaceFileSystem : IAsyncWorkspaceFileSystem
{
    private readonly InMemoryWorkspaceFileSystem _inner;
    private readonly HashSet<string> _failing = new(StringComparer.Ordinal);

    /// <summary>Creates the file system over a tree.</summary>
    /// <param name="inner">The tree.</param>
    public YieldingAsyncWorkspaceFileSystem(InMemoryWorkspaceFileSystem inner) => _inner = inner;

    /// <inheritdoc/>
    public string RootPath => _inner.RootPath;

    /// <summary>Gets the directories listed so far, in order.</summary>
    public IReadOnlyList<string> ListedDirectories => _inner.ListedDirectories;

    /// <summary>Makes listing a directory fail with an <see cref="IOException"/> from now on.</summary>
    /// <param name="relativeDirectory">The directory.</param>
    public void FailListing(string relativeDirectory) => _failing.Add(relativeDirectory);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkspaceEntry>> ListDirectoryAsync(string relativeDirectory, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        if (_failing.Contains(relativeDirectory))
        {
            throw new IOException("The remote machine did not answer.");
        }

        return _inner.ListDirectory(relativeDirectory);
    }
}
