namespace OmpOS.Harness.OmpLauncher;

/// <summary>
/// Finds the omp executable. Candidates are checked in a fixed order: the configured absolute path, then each search
/// directory in PATH order, then the known install locations. The first runnable file wins. Locating never throws for
/// a missing omp; a missing omp is reported as <see cref="OmpNotFound"/> with every path that was checked.
/// </summary>
public static class OmpLocator
{
    /// <summary>The name of the executable on this platform.</summary>
    public static string ExecutableName { get; } = OperatingSystem.IsWindows() ? "omp.exe" : "omp";

    /// <summary>The text returned with <see cref="OmpNotFound"/>.</summary>
    public const string InstallGuidance =
        "omp was not found. Install the omp CLI, then either put its directory on PATH or set the configured omp path " +
        "to the absolute path of the executable. The paths listed were checked in order.";

    /// <summary>
    /// Finds omp using <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The configured path, the search directories and the known install locations.</param>
    /// <returns><see cref="OmpFound"/> with the first runnable candidate, or <see cref="OmpNotFound"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">The configured path is set but is not absolute. That is a configuration error.</exception>
    public static OmpLocation Locate(OmpLocatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ConfiguredPath is not null && !Path.IsPathRooted(options.ConfiguredPath))
        {
            throw new ArgumentException("The configured omp path must be absolute.", nameof(options));
        }

        var tried = new List<string>();
        foreach (var candidate in Candidates(options).Distinct(StringComparer.Ordinal))
        {
            tried.Add(candidate);
            if (IsRunnableFile(candidate))
            {
                return new OmpFound(candidate);
            }
        }

        return new OmpNotFound(tried, InstallGuidance);
    }

    /// <summary>
    /// The known install locations for omp, checked after every PATH directory. The list is defined here, in this order:
    /// <list type="number">
    /// <item><description><c>$HOME/.bun/bin</c>: the bin directory of bun global installs.</description></item>
    /// <item><description><c>$HOME/.local/bin</c>: the per-user bin directory.</description></item>
    /// <item><description><c>/usr/local/bin</c>: the system-wide local bin directory.</description></item>
    /// <item><description><c>/usr/bin</c>: the system bin directory.</description></item>
    /// </list>
    /// The home entries are omitted when <paramref name="homeDirectory"/> is empty.
    /// </summary>
    /// <param name="homeDirectory">The user's home directory.</param>
    /// <returns>The absolute executable paths, in the order they are checked.</returns>
    public static IReadOnlyList<string> DefaultKnownInstallLocations(string homeDirectory)
    {
        var locations = new List<string>(capacity: 4);
        if (!string.IsNullOrWhiteSpace(homeDirectory))
        {
            locations.Add(Path.Combine(homeDirectory, ".bun", "bin", ExecutableName));
            locations.Add(Path.Combine(homeDirectory, ".local", "bin", ExecutableName));
        }

        locations.Add(Path.Combine("/usr/local/bin", ExecutableName));
        locations.Add(Path.Combine("/usr/bin", ExecutableName));
        return locations;
    }

    private static IEnumerable<string> Candidates(OmpLocatorOptions options)
    {
        if (options.ConfiguredPath is { } configured)
        {
            yield return configured;
        }

        // A relative PATH entry would resolve against the working directory, so it is never searched.
        foreach (var directory in options.SearchDirectories)
        {
            if (!string.IsNullOrWhiteSpace(directory) && Path.IsPathRooted(directory))
            {
                yield return Path.Combine(directory, ExecutableName);
            }
        }

        foreach (var known in options.KnownInstallLocations)
        {
            if (Path.IsPathRooted(known))
            {
                yield return known;
            }
        }
    }

    private static bool IsRunnableFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        const UnixFileMode anyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        return (File.GetUnixFileMode(path) & anyExecute) != UnixFileMode.None;
    }
}
