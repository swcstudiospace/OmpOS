namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>Finds, reads and merges the permission files of a user and a workspace.</summary>
public static class PermissionsLoader
{
    /// <summary>Loads the permissions. Files are read each call, so a head reloads after a file changes.</summary>
    /// <param name="homeDirectory">The home directory, or null to skip the user files.</param>
    /// <param name="projectRoot">The workspace root, or null to skip the project files.</param>
    /// <param name="trustProject">False to ignore the allow rules of project files.</param>
    /// <returns>The merged permissions.</returns>
    public static EffectivePermissions Load(string? homeDirectory, string? projectRoot, bool trustProject = true) =>
        LoadWithNotices(homeDirectory, projectRoot, trustProject).Permissions;

    /// <summary>Loads the permissions and the notices for any legacy file that was enforced or skipped.</summary>
    /// <param name="homeDirectory">The home directory, or null to skip the user files.</param>
    /// <param name="projectRoot">The workspace root, or null to skip the project files.</param>
    /// <param name="trustProject">False to ignore the allow rules of project files.</param>
    /// <returns>The merged permissions and the notices. A legacy file is skipped when the current file exists, and enforced when it is alone.</returns>
    public static (EffectivePermissions Permissions, IReadOnlyList<LegacySafetyNotice> Notices) LoadWithNotices(
        string? homeDirectory,
        string? projectRoot,
        bool trustProject = true)
    {
        var locations = SafetyFileLocations.Permissions(homeDirectory, projectRoot);
        var notices = LegacySafetyNotice.FromLocations(locations, SafetyFileLocations.PermissionsFileName);
        var documents = FilterShadowed(locations).Select(PermissionsFileReader.Read).ToList();
        return (EffectivePermissions.Merge(documents, trustProject), notices);
    }

    internal static IEnumerable<SafetyFileLocation> FilterShadowed(IReadOnlyList<SafetyFileLocation> locations)
    {
        foreach (var location in locations)
        {
            var folder = Path.GetFileName(Path.GetDirectoryName(location.Path));
            var shadowed = !string.Equals(folder, SafetyFileLocations.AutonogrammerFolder, StringComparison.Ordinal)
                && !string.Equals(folder, SafetyFileLocations.CursorFolder, StringComparison.Ordinal)
                && locations.Any(other =>
                    other.Scope == location.Scope
                    && string.Equals(Path.GetFileName(Path.GetDirectoryName(other.Path)), SafetyFileLocations.AutonogrammerFolder, StringComparison.Ordinal)
                    && File.Exists(other.Path));
            if (!shadowed)
            {
                yield return location;
            }
        }
    }
}
