namespace OmpOS.Presentation.Editor;

/// <summary>
/// One folder of a multi-root workspace as the explorer opens it when the folder is reached without blocking (an ssh host). The file system is
/// created by <paramref name="Open"/> when the root is opened, so a folder that cannot be reached fails only its own row.
/// </summary>
/// <param name="Id">A stable identifier of the root, unique in the workspace.</param>
/// <param name="DisplayName">The root name shown as the first level of the tree and used as the prefix of root-qualified paths. Unique ignoring case.</param>
/// <param name="IsPrimary">True for the root that is the working directory of the run.</param>
/// <param name="Open">Creates the file system of the root, for example a remote file system over an ssh profile.</param>
public sealed record AsyncWorkspaceRootSource(string Id, string DisplayName, bool IsPrimary, Func<CancellationToken, Task<IAsyncWorkspaceFileSystem>> Open)
{
    /// <summary>Makes an asynchronous source from a synchronous one: the folder is opened and listed on the thread pool.</summary>
    /// <param name="source">The synchronous source.</param>
    /// <returns>The equivalent asynchronous source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static AsyncWorkspaceRootSource FromSynchronous(WorkspaceRootSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new AsyncWorkspaceRootSource(
            source.Id,
            source.DisplayName,
            source.IsPrimary,
            cancellationToken => Task.Run<IAsyncWorkspaceFileSystem>(() => new AsyncWorkspaceFileSystemAdapter(source.Open()), cancellationToken));
    }
}
