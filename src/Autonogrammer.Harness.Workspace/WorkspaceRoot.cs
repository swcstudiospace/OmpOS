namespace Autonogrammer.Workspace;

/// <summary>
/// A workspace root directory. Every convention path and every setup path is resolved through
/// <see cref="Resolve"/>, which rejects any path that leaves the root, including through symbolic links.
/// </summary>
public sealed class WorkspaceRoot
{
    private const int MaxSymbolicLinkDepth = 40;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Creates a root for an existing directory.
    /// </summary>
    /// <param name="path">The workspace directory. It is stored as its real path, with symbolic links resolved.</param>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    public WorkspaceRoot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Workspace root '{fullPath}' does not exist.");
        }

        FullPath = ResolveRealPath(fullPath, depth: 0);
    }

    /// <summary>
    /// Gets the real absolute path of the root directory.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// Resolves a relative path inside the root.
    /// </summary>
    /// <param name="relativePath">A relative path. Both '/' and the platform separator are accepted.</param>
    /// <returns>The real absolute path of the target. The target need not exist.</returns>
    /// <exception cref="WorkspacePathException">
    /// The path is empty, absolute, contains NUL, leaves the root, or resolves through a symbolic link that leaves the root.
    /// </exception>
    public string Resolve(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        if (relativePath.Length == 0 || relativePath.Contains('\0', StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            throw new WorkspacePathException(relativePath, "it must be a non-empty relative path without NUL characters");
        }

        var lexical = Path.GetFullPath(Path.Combine(FullPath, relativePath));
        if (!IsWithinRoot(lexical))
        {
            throw new WorkspacePathException(relativePath, "it leaves the workspace root");
        }

        var real = ResolveRealPath(lexical, depth: 0);
        if (!IsWithinRoot(real))
        {
            throw new WorkspacePathException(relativePath, "it resolves through a symbolic link outside the workspace root");
        }

        return real;
    }

    private bool IsWithinRoot(string fullPath)
    {
        if (string.Equals(fullPath, FullPath, PathComparison))
        {
            return true;
        }

        var prefix = FullPath.EndsWith(Path.DirectorySeparatorChar)
            ? FullPath
            : FullPath + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, PathComparison);
    }

    /// <summary>
    /// Walks an absolute path from the file system root and replaces every symbolic link with its target,
    /// including links that are not yet resolved to an existing entry.
    /// </summary>
    private static string ResolveRealPath(string fullPath, int depth)
    {
        var current = Path.GetPathRoot(fullPath)
            ?? throw new IOException($"Cannot determine the root of '{fullPath}'.");
        var segments = fullPath[current.Length..]
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            var linkTarget = new FileInfo(current).LinkTarget;
            if (linkTarget is null)
            {
                continue;
            }

            if (depth >= MaxSymbolicLinkDepth)
            {
                throw new IOException($"Too many levels of symbolic links while resolving '{fullPath}'.");
            }

            var parent = Path.GetDirectoryName(current)
                ?? throw new IOException($"Cannot determine the parent of '{current}'.");
            current = ResolveRealPath(Path.GetFullPath(linkTarget, parent), depth + 1);
        }

        return current;
    }
}
