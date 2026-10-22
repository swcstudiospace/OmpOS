namespace OmpOS.Presentation.Editor;

/// <summary>
/// The workspace file system over a folder on disk. Listing never leaves the root: a path that resolves outside it, through a
/// symbolic link in any ancestor, is refused, and a link whose target lies outside is not listed at all. Links whose target
/// stays inside the root are listed like any other entry.
/// </summary>
public sealed class LocalWorkspaceFileSystem : IWorkspaceFileSystem
{
    private static readonly StringComparison RootComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string _rootFull;

    /// <summary>Opens a workspace over an existing folder.</summary>
    /// <param name="rootDirectory">The folder that becomes the workspace root.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rootDirectory"/> is null.</exception>
    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    public LocalWorkspaceFileSystem(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);

        _rootFull = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(_rootFull))
        {
            throw new DirectoryNotFoundException($"The workspace folder '{rootDirectory}' does not exist.");
        }
    }

    /// <inheritdoc/>
    public string RootPath => _rootFull;

    /// <inheritdoc/>
    public IReadOnlyList<WorkspaceEntry> ListDirectory(string relativeDirectory)
    {
        var normalized = WorkspacePath.Normalize(relativeDirectory);
        var directory = Path.GetFullPath(Path.Combine(_rootFull, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(directory) || HasSymlinkEscape(directory))
        {
            throw new UnauthorizedAccessException("The directory resolves outside the workspace root.");
        }

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"No directory exists at '{relativeDirectory}'.");
        }

        var entries = new List<WorkspaceEntry>();
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            var name = Path.GetFileName(path);
            var child = Path.Combine(directory, name);
            if (!IsWithinRoot(child) || HasSymlinkEscape(child))
            {
                continue;
            }

            var relativePath = WorkspacePath.Combine(normalized, name);
            if (Directory.Exists(child))
            {
                entries.Add(new WorkspaceEntry(name, relativePath, IsDirectory: true));
            }
            else if (File.Exists(child))
            {
                entries.Add(new WorkspaceEntry(name, relativePath, IsDirectory: false));
            }
        }

        return entries;
    }

    private bool IsWithinRoot(string candidate)
    {
        if (candidate.Length < _rootFull.Length || !candidate.StartsWith(_rootFull, RootComparison))
        {
            return false;
        }

        // A filesystem root such as "/" already ends with a separator, so the character after it is the child's first.
        return candidate.Length == _rootFull.Length
            || Path.EndsInDirectorySeparator(_rootFull)
            || candidate[_rootFull.Length] == Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Checks the path and every existing ancestor down to the root for a link whose final target leaves the root.
    /// Ancestors that do not exist yet cannot be links.
    /// </summary>
    private bool HasSymlinkEscape(string fullPath)
    {
        var current = fullPath;
        while (current is not null
            && !string.Equals(current, _rootFull, RootComparison)
            && IsWithinRoot(current))
        {
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.Exists)
            {
                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null && !IsWithinRoot(Path.GetFullPath(target.FullName)))
                {
                    return true;
                }
            }

            current = Path.GetDirectoryName(current);
        }

        return false;
    }
}
