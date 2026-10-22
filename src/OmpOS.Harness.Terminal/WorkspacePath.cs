using System.Diagnostics.CodeAnalysis;

namespace OmpOS.Harness.Terminal;

/// <summary>
/// Resolves a declared working directory to its physical location and checks that it stays inside the workspace root.
/// Symbolic links are followed one segment at a time, as the operating system follows them, so a link inside the root
/// that points outside it is rejected, and a <c>..</c> after a link is applied to the link's target.
/// </summary>
internal static class WorkspacePath
{
    private const int MaxLinkHops = 40;

    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    /// Returns the physical absolute path of an existing workspace root.
    /// </summary>
    /// <param name="root">The root as configured.</param>
    /// <returns>The root with every symbolic link resolved.</returns>
    /// <exception cref="DirectoryNotFoundException">The root does not exist.</exception>
    public static string ResolveRoot(string root)
    {
        var full = Path.GetFullPath(root);
        if (!Directory.Exists(full))
        {
            throw new DirectoryNotFoundException($"Workspace root '{full}' does not exist.");
        }

        return Resolve(full) ?? throw new InvalidOperationException($"Workspace root '{full}' has a symbolic link loop.");
    }

    /// <summary>
    /// Resolves a declared working directory and reports whether it is an existing directory inside the root.
    /// </summary>
    /// <param name="canonicalRoot">A root returned by <see cref="ResolveRoot"/>.</param>
    /// <param name="workingDirectory">The declared directory: relative to the root, or absolute. Empty means the root.</param>
    /// <param name="physicalPath">The physical path when the result is true.</param>
    /// <returns>True when the directory exists and its physical location is the root or inside it.</returns>
    public static bool TryResolveInside(
        string canonicalRoot,
        string workingDirectory,
        [NotNullWhen(true)] out string? physicalPath)
    {
        physicalPath = null;
        var declared = Path.IsPathFullyQualified(workingDirectory)
            ? workingDirectory
            : Path.Combine(canonicalRoot, workingDirectory);

        var resolved = Resolve(declared);
        if (resolved is null || !IsWithin(canonicalRoot, resolved) || !Directory.Exists(resolved))
        {
            return false;
        }

        physicalPath = resolved;
        return true;
    }

    /// <summary>
    /// Walks an absolute path from its root, following symbolic links and applying <c>..</c> to the physical parent.
    /// </summary>
    /// <returns>The physical path, or null when the path is not absolute or the link chain is too long.</returns>
    private static string? Resolve(string absolutePath)
    {
        if (!Path.IsPathFullyQualified(absolutePath))
        {
            return null;
        }

        var current = Path.GetPathRoot(absolutePath)!;
        var pending = new Stack<string>();
        PushSegments(pending, absolutePath[current.Length..]);

        var hops = 0;
        while (pending.Count > 0)
        {
            var segment = pending.Pop();
            if (string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                current = Path.GetDirectoryName(current) ?? current;
                continue;
            }

            var next = Path.Combine(current, segment);
            var linkTarget = LinkTargetOf(next);
            if (linkTarget is null)
            {
                current = next;
                continue;
            }

            if (++hops > MaxLinkHops)
            {
                return null;
            }

            if (Path.IsPathFullyQualified(linkTarget))
            {
                current = Path.GetPathRoot(linkTarget)!;
                PushSegments(pending, linkTarget[current.Length..]);
            }
            else
            {
                PushSegments(pending, linkTarget);
            }
        }

        return current;
    }

    private static void PushSegments(Stack<string> pending, string path)
    {
        var segments = path.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        for (var index = segments.Length - 1; index >= 0; index--)
        {
            pending.Push(segments[index]);
        }
    }

    private static string? LinkTargetOf(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        return info.LinkTarget;
    }

    private static bool IsWithin(string root, string path)
    {
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (string.Equals(path, root, comparison))
        {
            return true;
        }

        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, comparison);
    }
}
