using System.Globalization;
using System.Text;

namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// Presents a synchronous <see cref="IWorkspaceFileSystem"/> (the local disk) as an <see cref="IAsyncWorkspaceFileSystem"/>. Every call runs on
/// the thread pool, so the caller is never blocked by the disk. The behaviour is that of the wrapped file system; the read additionally
/// refuses a file whose UTF-8 size exceeds the cap, after reading it (the local disk has no cheaper way to know).
/// </summary>
public sealed class AsyncWorkspaceFileSystemAdapter : IAsyncWorkspaceFileSystem
{
    private readonly IWorkspaceFileSystem _inner;

    /// <summary>Initializes a new instance of the <see cref="AsyncWorkspaceFileSystemAdapter"/> class.</summary>
    /// <param name="inner">The file system to wrap.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    public AsyncWorkspaceFileSystemAdapter(IWorkspaceFileSystem inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc/>
    public string RootPath => _inner.RootPath;

    /// <inheritdoc/>
    public Task<IReadOnlyList<WorkspaceEntry>> ListDirectoryAsync(string relativeDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relativeDirectory);
        return Task.Run(() => _inner.ListDirectory(relativeDirectory), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> ReadFileAsync(string relativePath, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        return Task.Run(
            () =>
            {
                var text = _inner.ReadFile(relativePath);
                if (Encoding.UTF8.GetByteCount(text) > maxBytes)
                {
                    throw new WorkspaceFileTooLargeException(string.Create(CultureInfo.InvariantCulture, $"'{relativePath}' holds more than {maxBytes} bytes."));
                }

                return text;
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<WorkspaceFileStat?> StatAsync(string relativePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        return Task.Run(() => Stat(relativePath), cancellationToken);
    }

    /// <inheritdoc/>
    public Task WriteFileAsync(string relativePath, string content, string expectedCurrent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(expectedCurrent);
        return Task.Run(() => _inner.WriteFile(relativePath, content, expectedCurrent), cancellationToken);
    }

    /// <inheritdoc/>
    public Task CreateFileAsync(string relativePath, string content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        return Task.Run(() => _inner.CreateFile(relativePath, content), cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteFileAsync(string relativePath, string expectedCurrent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(expectedCurrent);
        return Task.Run(() => _inner.DeleteFile(relativePath, expectedCurrent), cancellationToken);
    }

    // The wrapped interface has no stat: the entry is found in the listing of its parent. Size and time are not known.
    private WorkspaceFileStat? Stat(string relativePath)
    {
        var normalized = WorkspacePath.Normalize(relativePath);
        if (normalized.Length == 0)
        {
            return new WorkspaceFileStat(IsDirectory: true, Length: null, ModifiedUtc: null);
        }

        var slash = normalized.LastIndexOf('/');
        var parent = slash < 0 ? string.Empty : normalized[..slash];
        IReadOnlyList<WorkspaceEntry> siblings;
        try
        {
            siblings = _inner.ListDirectory(parent);
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        foreach (var entry in siblings)
        {
            if (string.Equals(entry.RelativePath, normalized, StringComparison.Ordinal))
            {
                return new WorkspaceFileStat(entry.IsDirectory, Length: null, ModifiedUtc: null);
            }
        }

        return null;
    }
}
