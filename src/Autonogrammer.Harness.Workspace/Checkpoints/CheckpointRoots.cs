namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>The workspace folders a checkpoint store may record in and write to. Every path is checked against them with symbolic links resolved, so nothing outside them is ever read for a rewind or written.</summary>
internal sealed class CheckpointRoots
{
    private readonly IReadOnlyList<(string Given, WorkspaceRoot Root)> _roots;

    /// <summary>Opens the folders.</summary>
    /// <param name="paths">The workspace folders, the primary first.</param>
    /// <exception cref="ArgumentException">There is no folder.</exception>
    /// <exception cref="DirectoryNotFoundException">A folder does not exist.</exception>
    public CheckpointRoots(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _roots = [.. paths.Select(static path => (Path.GetFullPath(path), new WorkspaceRoot(path)))];
        if (_roots.Count == 0)
        {
            throw new ArgumentException("A checkpoint store needs at least one workspace folder.", nameof(paths));
        }
    }

    /// <summary>Finds the folder that holds a file and the file's path inside it.</summary>
    /// <param name="path">An absolute path, or a path relative to the primary folder.</param>
    /// <param name="root">The real path of the folder.</param>
    /// <param name="relative">The path inside the folder, with forward slashes.</param>
    /// <returns><see langword="false"/> when the path is outside every folder, names a folder itself or leaves a folder through a symbolic link.</returns>
    public bool TryLocate(string path, out string root, out string relative)
    {
        root = string.Empty;
        relative = string.Empty;
        var full = Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(path, _roots[0].Given);
        foreach (var (given, workspaceRoot) in _roots)
        {
            foreach (var basePath in new[] { given, workspaceRoot.FullPath })
            {
                var candidate = Path.GetRelativePath(basePath, full);
                if (candidate is "." or ".." || Path.IsPathRooted(candidate) || candidate.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryResolve(workspaceRoot, candidate))
                {
                    root = workspaceRoot.FullPath;
                    relative = candidate.Replace('\\', '/');
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Resolves a recorded file to its real absolute path.</summary>
    /// <param name="root">The real path of the folder the file was recorded in.</param>
    /// <param name="relative">The path inside the folder.</param>
    /// <param name="reason">Why the file is refused; empty on success.</param>
    /// <returns>The absolute path, or <see langword="null"/> when the folder is not open now or the path leaves it.</returns>
    public string? Resolve(string root, string relative, out string reason)
    {
        reason = string.Empty;
        WorkspaceRoot? match = null;
        foreach (var (_, candidate) in _roots)
        {
            if (string.Equals(candidate.FullPath, root, StringComparison.Ordinal))
            {
                match = candidate;
                break;
            }
        }

        if (match is null)
        {
            reason = "Its workspace folder is not open, so the file is left alone.";
            return null;
        }

        try
        {
            return match.Resolve(relative.Replace('/', Path.DirectorySeparatorChar));
        }
        catch (WorkspacePathException)
        {
            reason = "Its path leaves the workspace folder, so the file is left alone.";
            return null;
        }
    }

    private static bool TryResolve(WorkspaceRoot root, string relative)
    {
        try
        {
            _ = root.Resolve(relative);
            return true;
        }
        catch (WorkspacePathException)
        {
            return false;
        }
    }
}
