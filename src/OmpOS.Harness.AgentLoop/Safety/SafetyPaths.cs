namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>
/// Path handling for the decision engine: <c>~</c> expansion, real paths with every symlink resolved (so a link inside the workspace
/// that points outside is outside), and containment. Paths are compared in a portable form with <c>/</c> separators. A path that
/// cannot be resolved (a link loop, an unreadable folder) resolves to null and is treated as outside every boundary.
/// </summary>
internal sealed class SafetyPaths
{
    private const int MaxLinkHops = 40;

    private readonly StringComparison _comparison;

    /// <summary>Initializes a new instance of the <see cref="SafetyPaths"/> class.</summary>
    /// <param name="workspaceRoot">The absolute workspace root.</param>
    /// <param name="homeDirectory">The home directory <c>~</c> expands to, or null for none.</param>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is blank.</exception>
    public SafetyPaths(string workspaceRoot, string? homeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        IgnoreCase = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
        _comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var fullRoot = Path.GetFullPath(workspaceRoot);
        WorkspaceRoot = Portable(fullRoot);
        RealWorkspaceRoot = RealNative(fullRoot) ?? WorkspaceRoot;
        Home = string.IsNullOrWhiteSpace(homeDirectory) ? null : Portable(Path.GetFullPath(homeDirectory));
    }

    /// <summary>Gets a value indicating whether paths compare without regard to case on this platform.</summary>
    public bool IgnoreCase { get; }

    /// <summary>Gets the workspace root as written, absolute and portable.</summary>
    public string WorkspaceRoot { get; }

    /// <summary>Gets the workspace root with every symlink resolved.</summary>
    public string RealWorkspaceRoot { get; }

    /// <summary>Gets the home directory, or null when none is known.</summary>
    public string? Home { get; }

    /// <summary>Returns whether a path is a folder or file inside (or equal to) a root. Both are portable and absolute.</summary>
    /// <param name="path">The path.</param>
    /// <param name="root">The root.</param>
    /// <returns>True when <paramref name="path"/> is <paramref name="root"/> or below it.</returns>
    public bool IsUnder(string path, string root)
    {
        var folder = root.TrimEnd('/');
        return string.Equals(path, folder.Length == 0 ? "/" : folder, _comparison) || path.StartsWith(folder + "/", _comparison);
    }

    /// <summary>Returns whether a real path is inside the real workspace root.</summary>
    /// <param name="realPath">The real path, or null for an unresolvable path.</param>
    /// <returns>True when it is inside.</returns>
    public bool IsInWorkspace(string? realPath) => realPath is not null && IsUnder(realPath, RealWorkspaceRoot);

    /// <summary>Expands a leading <c>~</c> to the home directory.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The expanded path; a <c>~</c> with no home directory is left as it is.</returns>
    public string ExpandHome(string path)
    {
        if (Home is null || path.Length == 0 || path[0] != '~')
        {
            return path;
        }

        if (path.Length == 1)
        {
            return Home;
        }

        return path[1] is '/' or '\\' ? Home.TrimEnd('/') + "/" + path[2..] : path;
    }

    /// <summary>Makes a path absolute without touching the file system: <c>~</c> expanded, relative paths joined to the working directory, <c>.</c> and <c>..</c> collapsed.</summary>
    /// <param name="path">The path.</param>
    /// <param name="workingDirectory">The directory relative paths start from; null means the workspace root.</param>
    /// <returns>The portable absolute path.</returns>
    public string Lexical(string path, string? workingDirectory)
    {
        var joined = Join(path, workingDirectory);
        try
        {
            return Portable(Path.GetFullPath(joined));
        }
        catch (ArgumentException)
        {
            return Portable(joined);
        }
        catch (NotSupportedException)
        {
            return Portable(joined);
        }
    }

    /// <summary>Resolves a path to the file it really names: every symlink followed, <c>..</c> applied after the links before it.</summary>
    /// <param name="path">The path.</param>
    /// <param name="workingDirectory">The directory relative paths start from; null means the workspace root.</param>
    /// <returns>The portable real path, or null when it cannot be resolved.</returns>
    public string? Real(string path, string? workingDirectory) => RealNative(Join(path, workingDirectory));

    /// <summary>Converts a native path to the portable form: <c>/</c> separators.</summary>
    /// <param name="path">The native path.</param>
    /// <returns>The portable path.</returns>
    public static string Portable(string path) => Path.DirectorySeparatorChar == '/' ? path : path.Replace(Path.DirectorySeparatorChar, '/');

    private static string? RealNative(string absolute)
    {
        var root = Path.GetPathRoot(absolute);
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        var current = root;
        var pending = new Stack<string>();
        Push(pending, absolute[root.Length..]);
        var hops = 0;
        while (pending.Count > 0)
        {
            var part = pending.Pop();
            if (part.Length == 0 || string.Equals(part, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(part, "..", StringComparison.Ordinal))
            {
                var parent = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                current = string.IsNullOrEmpty(parent) ? root : parent;
                continue;
            }

            var next = Path.Combine(current, part);
            if (!TryLinkTarget(next, out var target))
            {
                return null;
            }

            if (target is null)
            {
                current = next;
                continue;
            }

            if (++hops > MaxLinkHops)
            {
                return null;
            }

            var resolved = Path.IsPathRooted(target) ? target : Path.Combine(current, target);
            var resolvedRoot = Path.GetPathRoot(resolved);
            if (string.IsNullOrEmpty(resolvedRoot))
            {
                return null;
            }

            Push(pending, resolved[resolvedRoot.Length..]);
            current = resolvedRoot;
        }

        return Portable(current);
    }

    private static bool TryLinkTarget(string path, out string? target)
    {
        target = null;
        try
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            target = info.LinkTarget;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static void Push(Stack<string> pending, string rest)
    {
        var parts = rest.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        for (var index = parts.Length - 1; index >= 0; index--)
        {
            pending.Push(parts[index]);
        }
    }

    private string Join(string path, string? workingDirectory)
    {
        var expanded = ExpandHome(path);
        if (Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        var baseDirectory = string.IsNullOrEmpty(workingDirectory) ? WorkspaceRoot : ExpandHome(workingDirectory);
        if (!Path.IsPathRooted(baseDirectory))
        {
            baseDirectory = Path.Combine(WorkspaceRoot, baseDirectory);
        }

        return Path.Combine(baseDirectory, expanded);
    }
}
