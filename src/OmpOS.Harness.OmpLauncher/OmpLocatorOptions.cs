namespace OmpOS.Harness.OmpLauncher;

/// <summary>
/// The inputs to <see cref="OmpLocator.Locate"/>.
/// </summary>
/// <param name="ConfiguredPath">An absolute path from configuration, or null when none is configured.</param>
/// <param name="SearchDirectories">The directories searched for omp, in order. Empty and relative entries are skipped.</param>
/// <param name="KnownInstallLocations">Absolute executable paths checked after the search directories, in order.</param>
public sealed record OmpLocatorOptions(
    string? ConfiguredPath,
    IReadOnlyList<string> SearchDirectories,
    IReadOnlyList<string> KnownInstallLocations)
{
    /// <summary>
    /// Builds options from the process environment: PATH supplies the search directories, the user's profile directory
    /// supplies the known install locations.
    /// </summary>
    /// <param name="configuredPath">The configured absolute path, or null.</param>
    /// <returns>The options.</returns>
    public static OmpLocatorOptions FromEnvironment(string? configuredPath)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new OmpLocatorOptions(
            configuredPath,
            path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries),
            OmpLocator.DefaultKnownInstallLocations(home));
    }
}
