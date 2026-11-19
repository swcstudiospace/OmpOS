using System.Security;

namespace Autonogrammer.Native.Agents;

/// <summary>
/// Decides which directory the agent treats as its workspace when the application starts. A terminal launch inside a project keeps that
/// project. A shortcut, Finder or double-click launch often starts the process in the install folder, in <c>System32</c>, in a
/// filesystem root (<c>/</c> or a drive root) or in another system location that a normal user cannot write to; those are replaced by
/// the user's home directory.
/// </summary>
/// <remarks>
/// Limitation: paths are compared lexically, after full-path normalisation and with the platform's case rule. Symbolic links, junctions
/// and 8.3 short names are not resolved, so a directory reached through such an alias of the install folder or of a system location
/// is not recognised as being inside it.
/// </remarks>
internal static class LaunchDirectory
{
    /// <summary>
    /// Resolves the workspace directory from the real process: the current directory, <see cref="AppContext.BaseDirectory"/>, the system
    /// folders reported by <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/> and the user's profile folder.
    /// </summary>
    /// <returns>The directory to use as the workspace root. It never throws.</returns>
    internal static string Resolve() =>
        Resolve(
            ReadCurrentDirectory(),
            AppContext.BaseDirectory,
            SystemProtectedRoots(),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Resolves the workspace directory with the platform's path comparison.</summary>
    /// <param name="currentDirectory">The process's current directory, possibly missing or empty.</param>
    /// <param name="baseDirectory">The directory the application runs from. It and everything below it are refused.</param>
    /// <param name="protectedRoots">System locations that are refused together with everything below them.</param>
    /// <param name="homeDirectory">The user's home directory, returned when the current directory is refused.</param>
    /// <returns>The full current directory when it is usable, otherwise <paramref name="homeDirectory"/>. It never throws.</returns>
    internal static string Resolve(string? currentDirectory, string? baseDirectory, IReadOnlyList<string>? protectedRoots, string? homeDirectory) =>
        ResolveWith(currentDirectory, baseDirectory, protectedRoots, homeDirectory, PlatformComparison);

    /// <summary>Resolves the workspace directory with an explicit path comparison, so each platform's rule can be checked on any host.</summary>
    /// <param name="currentDirectory">The process's current directory, possibly missing or empty.</param>
    /// <param name="baseDirectory">The directory the application runs from. It and everything below it are refused.</param>
    /// <param name="protectedRoots">System locations that are refused together with everything below them.</param>
    /// <param name="homeDirectory">The user's home directory, returned when the current directory is refused.</param>
    /// <param name="comparison">How two paths are compared: ordinal on Linux, ignoring case on Windows and macOS.</param>
    /// <returns>The full current directory without a trailing separator when it is usable, otherwise <paramref name="homeDirectory"/>. It never throws.</returns>
    internal static string ResolveWith(
        string? currentDirectory,
        string? baseDirectory,
        IReadOnlyList<string>? protectedRoots,
        string? homeDirectory,
        StringComparison comparison)
    {
        var home = string.IsNullOrWhiteSpace(homeDirectory) ? Path.TrimEndingDirectorySeparator(Path.GetTempPath()) : homeDirectory;
        var current = Normalize(currentDirectory);
        if (current is null || IsFilesystemRoot(current, comparison) || !Directory.Exists(current))
        {
            return home;
        }

        if (IsSameOrBelow(current, Normalize(baseDirectory), comparison))
        {
            return home;
        }

        if (protectedRoots is not null)
        {
            foreach (var root in protectedRoots)
            {
                if (IsSameOrBelow(current, Normalize(root), comparison))
                {
                    return home;
                }
            }
        }

        return current;
    }

    /// <summary>
    /// Lists the system locations the running platform keeps out of reach of a normal user. On Unix these are exact system directories
    /// nobody keeps projects in; <c>/usr</c> and <c>/opt</c> themselves are not listed, so a project below <c>/usr/local</c> or <c>/opt</c> is kept.
    /// </summary>
    /// <returns>The non-empty locations.</returns>
    internal static IReadOnlyList<string> SystemProtectedRoots()
    {
        if (!OperatingSystem.IsWindows())
        {
            return ["/bin", "/sbin", "/lib", "/usr/bin", "/usr/sbin", "/usr/lib", "/System", "/Applications"];
        }

        var folders = new[]
        {
            Environment.SpecialFolder.Windows,
            Environment.SpecialFolder.System,
            Environment.SpecialFolder.SystemX86,
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
        };
        var roots = new List<string>(folders.Length);
        foreach (var folder in folders)
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(path))
            {
                roots.Add(path);
            }
        }

        return roots;
    }

    private static StringComparison PlatformComparison =>
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private static string ReadCurrentDirectory()
    {
        try
        {
            return Environment.CurrentDirectory;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return string.Empty;
        }
    }

    private static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or SecurityException or IOException)
        {
            return null;
        }
    }

    private static bool IsFilesystemRoot(string normalized, StringComparison comparison)
    {
        var root = Path.GetPathRoot(normalized);
        return !string.IsNullOrEmpty(root) && string.Equals(Path.TrimEndingDirectorySeparator(root), normalized, comparison);
    }

    private static bool IsSameOrBelow(string candidate, string? root, StringComparison comparison)
    {
        if (root is null)
        {
            return false;
        }

        if (string.Equals(candidate, root, comparison))
        {
            return true;
        }

        var prefix = root.Length > 0 && IsSeparator(root[^1]) ? root : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, comparison);
    }

    private static bool IsSeparator(char c) => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
}
