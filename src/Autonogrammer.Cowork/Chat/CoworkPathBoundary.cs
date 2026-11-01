namespace Autonogrammer.Cowork.Chat;

/// <summary>
/// Refuses secrets, <c>~/.omp</c>, <c>~/.ssh</c>, account folders, <c>/</c>, the home directory itself and system directories as trusted folders,
/// and canonicalises symlinks and <c>..</c>. A UNC path or a drive root is never a trusted folder.
/// </summary>
public sealed class CoworkPathBoundary
{
    private readonly string _stateRoot;
    private readonly string _home;

    /// <summary>Initializes a new instance of the <see cref="CoworkPathBoundary"/> class.</summary>
    /// <param name="stateRoot">The state directory.</param>
    /// <param name="homeDirectory">The home directory used to expand <c>~</c>. Null uses the current user profile.</param>
    /// <exception cref="ArgumentException"><paramref name="stateRoot"/> is missing.</exception>
    public CoworkPathBoundary(string stateRoot, string? homeDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRoot);
        _stateRoot = Path.GetFullPath(stateRoot);
        _home = string.IsNullOrWhiteSpace(homeDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : Path.GetFullPath(homeDirectory);
    }

    /// <summary>Gets the secrets directory that cannot be trusted.</summary>
    public string SecretsDirectory => Path.Combine(_stateRoot, "secrets");

    /// <summary>Gets the account-folder root that cannot be trusted.</summary>
    public string AccountRoot => Path.Combine(_stateRoot, CoworkAccountFolder.RootName);

    /// <summary>Gets <c>~/.omp</c>.</summary>
    public string OmpDirectory => Path.Combine(_home, ".omp");

    /// <summary>Gets <c>~/.ssh</c>.</summary>
    public string SshDirectory => Path.Combine(_home, ".ssh");

    /// <summary>Whether the path is a UNC path (<c>\\server\share</c> or <c>//server/share</c>).</summary>
    /// <param name="path">The path.</param>
    /// <returns>True when the path is UNC.</returns>
    public static bool IsUncPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);
    }

    /// <summary>Whether the path is a Windows drive root such as <c>C:\</c>.</summary>
    /// <param name="path">The path.</param>
    /// <returns>True when the path is a drive root.</returns>
    public static bool IsDriveRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.TrimEnd('\\', '/');
        return trimmed.Length == 2 && char.IsAsciiLetter(trimmed[0]) && trimmed[1] == ':';
    }

    /// <summary>
    /// Canonicalises a path: expands <c>~</c>, resolves <c>..</c>, and follows symlinks that exist.
    /// A UNC path or a drive root is returned unchanged so the refusal does not depend on a Windows filesystem.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The canonical path.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is missing.</exception>
    public string Canonicalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (IsUncPath(path) || IsDriveRoot(path))
        {
            return path;
        }

        var expanded = ExpandHome(path);
        var full = Path.GetFullPath(expanded);
        return ResolveLinks(full);
    }

    /// <summary>The refusal for <c>/</c>, the home directory itself, a system directory, or a symlink that resolves into one.</summary>
    public const string FolderRefusedProtected = "FolderRefused(protected)";

    private static readonly string[] SystemDirectories =
    [
        "/bin", "/sbin", "/usr", "/etc", "/var", "/lib", "/lib64", "/boot", "/proc", "/sys", "/dev", "/opt", "/run",
        "/System", "/Library", "/private",
        "/Windows", "/Program Files", "/Program Files (x86)",
    ];

    /// <summary>Whether this path cannot be a trusted folder.</summary>
    /// <param name="path">The candidate folder.</param>
    /// <returns>True when the path is refused.</returns>
    public bool IsForbiddenTrustedFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsUncPath(path) || IsDriveRoot(path) || IsProtectedTrustedFolder(path))
        {
            return true;
        }

        var canonical = Canonicalize(path);
        if (IsUncPath(canonical) || IsDriveRoot(canonical) || IsProtectedTrustedFolder(canonical))
        {
            return true;
        }

        return IsUnder(canonical, SecretsDirectory)
            || IsUnder(canonical, OmpDirectory)
            || IsUnder(canonical, SshDirectory)
            || IsUnder(canonical, AccountRoot);
    }

    /// <summary>Whether the path is <c>/</c>, the home directory itself, or a system directory after canonicalisation.</summary>
    /// <param name="path">The candidate folder.</param>
    /// <returns>True when the path is protected.</returns>
    public bool IsProtectedTrustedFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsUncPath(path) || IsDriveRoot(path))
        {
            return true;
        }

        var canonical = Canonicalize(path);
        if (IsUnixRoot(canonical) || IsDriveRoot(canonical))
        {
            return true;
        }

        if (string.Equals(Trim(canonical), Trim(_home), StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var system in SystemDirectories)
        {
            if (IsUnder(canonical, system))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The refusal token for a trusted-folder candidate, or null when it is accepted.</summary>
    /// <param name="path">The candidate folder.</param>
    /// <returns><see cref="FolderRefusedProtected"/> for a protected path, <c>FolderRefused</c> for the other refusals, or null.</returns>
    public string? TrustedFolderRefusal(string path) =>
        IsProtectedTrustedFolder(path) ? FolderRefusedProtected : IsForbiddenTrustedFolder(path) ? "FolderRefused" : null;

    /// <summary>Whether a write is inside a read-write grant or the session scratch directory.</summary>
    /// <param name="path">The path.</param>
    /// <param name="readWriteGrants">The read-write grant folders.</param>
    /// <param name="scratchDirectory">The session scratch directory, or null.</param>
    /// <param name="workingDirectory">The directory a relative path is resolved against.</param>
    /// <returns>True when the write is inside a read-write grant or the scratch directory.</returns>
    public bool IsWriteAllowed(string path, IReadOnlyList<string> readWriteGrants, string? scratchDirectory, string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(readWriteGrants);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        var absolute = Path.IsPathRooted(path) || path.StartsWith('~') || IsUncPath(path)
            ? path
            : Path.Combine(workingDirectory, path);
        var canonical = Canonicalize(absolute);
        if (scratchDirectory is not null && !IsForbiddenTrustedFolder(scratchDirectory) && IsUnder(canonical, Canonicalize(scratchDirectory)))
        {
            return true;
        }

        foreach (var grant in readWriteGrants)
        {
            if (IsForbiddenTrustedFolder(grant))
            {
                continue;
            }

            if (IsUnder(canonical, Canonicalize(grant)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether every candidate is an allowed trusted folder.</summary>
    /// <param name="folders">The folders.</param>
    /// <param name="refusal">The first refused path, or null.</param>
    /// <returns>False when any folder is forbidden.</returns>
    public bool TryAcceptTrustedFolders(IReadOnlyList<string> folders, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(folders);
        foreach (var folder in folders)
        {
            if (IsForbiddenTrustedFolder(folder))
            {
                refusal = folder;
                return false;
            }
        }

        refusal = null;
        return true;
    }

    /// <summary>
    /// Whether the path is inside a trusted folder or the Storage folder after canonicalisation.
    /// A path that lands in a forbidden directory is outside even when a symlink made it look inside.
    /// </summary>
    /// <param name="path">The path a tool asked to use.</param>
    /// <param name="trustedFolders">The trusted folders.</param>
    /// <param name="storageFolder">The Storage folder, or null when the session has none.</param>
    /// <param name="workingDirectory">The directory a relative path is resolved against.</param>
    /// <returns>True when the canonical path is inside an allowed folder and not forbidden.</returns>
    public bool IsAllowed(string path, IReadOnlyList<string> trustedFolders, string? storageFolder, string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(trustedFolders);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        var absolute = Path.IsPathRooted(path) || path.StartsWith('~') || IsUncPath(path)
            ? path
            : Path.Combine(workingDirectory, path);
        if (IsForbiddenTrustedFolder(absolute) && !IsStorage(absolute, storageFolder))
        {
            // Forbidden wins except a Storage folder that was itself accepted. Storage under secrets is still forbidden.
            return false;
        }

        var canonical = Canonicalize(absolute);
        if (IsUnderAnyForbidden(canonical))
        {
            return false;
        }

        if (storageFolder is not null && IsUnder(canonical, Canonicalize(storageFolder)))
        {
            return true;
        }

        foreach (var folder in trustedFolders)
        {
            if (IsForbiddenTrustedFolder(folder))
            {
                continue;
            }

            if (IsUnder(canonical, Canonicalize(folder)))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsStorage(string path, string? storageFolder) =>
        storageFolder is not null && !IsForbiddenTrustedFolder(storageFolder) && IsUnder(Canonicalize(path), Canonicalize(storageFolder));

    private bool IsUnderAnyForbidden(string canonical) =>
        IsUnder(canonical, SecretsDirectory)
        || IsUnder(canonical, OmpDirectory)
        || IsUnder(canonical, SshDirectory)
        || IsUnder(canonical, AccountRoot);

    private string ExpandHome(string path)
    {
        if (string.Equals(path, "~", StringComparison.Ordinal))
        {
            return _home;
        }

        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(_home, path[2..]);
        }

        return path;
    }

    private static string ResolveLinks(string full)
    {
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root))
        {
            return full;
        }

        var relative = Path.GetRelativePath(root, full);
        if (relative is "." or "")
        {
            return root;
        }

        var current = root;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (segment is "" or ".")
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                var parent = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar));
                current = string.IsNullOrEmpty(parent) ? root : parent;
                continue;
            }

            current = Path.Combine(current, segment);
            var resolved = ResolveOne(current);
            if (resolved is not null)
            {
                current = resolved;
            }
        }

        return current;
    }

    private static string? ResolveOne(string path)
    {
        FileSystemInfo info = File.Exists(path) ? new FileInfo(path) : new DirectoryInfo(path);
        if (!info.Exists)
        {
            return null;
        }

        return info.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
    }

    private static bool IsUnixRoot(string canonical)
    {
        var trimmed = Trim(canonical);
        return trimmed.Length == 0 || trimmed is "/" or "\\";
    }

    private static string Trim(string path) => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool IsUnder(string canonical, string root)
    {
        var fullRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (fullRoot.Length == 0)
        {
            return false;
        }

        if (string.Equals(canonical.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), fullRoot, StringComparison.Ordinal))
        {
            return true;
        }

        var prefix = fullRoot + Path.DirectorySeparatorChar;
        return canonical.StartsWith(prefix, StringComparison.Ordinal);
    }
}
