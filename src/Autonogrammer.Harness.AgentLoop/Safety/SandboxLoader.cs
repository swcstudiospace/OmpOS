namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>Finds, reads and merges the sandbox files of a user and a workspace.</summary>
public static class SandboxLoader
{
    /// <summary>Loads the sandbox. Files are read each call, so a head reloads after a file changes.</summary>
    /// <param name="homeDirectory">The home directory, or null to skip the user files.</param>
    /// <param name="projectRoot">The workspace root, or null to skip the project files.</param>
    /// <param name="settings">The Settings values the files replace, or null for the defaults.</param>
    /// <param name="trustProject">False to apply only the strict parts of project files.</param>
    /// <returns>The sandbox in force.</returns>
    public static EffectiveSandbox Load(string? homeDirectory, string? projectRoot, SandboxSettings? settings = null, bool trustProject = true) =>
        LoadWithNotices(homeDirectory, projectRoot, settings, trustProject).Sandbox;

    /// <summary>Loads the sandbox and the notices for any legacy file that was enforced or skipped.</summary>
    /// <param name="homeDirectory">The home directory, or null to skip the user files.</param>
    /// <param name="projectRoot">The workspace root, or null to skip the project files.</param>
    /// <param name="settings">The Settings values the files replace, or null for the defaults.</param>
    /// <param name="trustProject">False to apply only the strict parts of project files.</param>
    /// <returns>The sandbox in force and the notices.</returns>
    public static (EffectiveSandbox Sandbox, IReadOnlyList<LegacySafetyNotice> Notices) LoadWithNotices(
        string? homeDirectory,
        string? projectRoot,
        SandboxSettings? settings = null,
        bool trustProject = true)
    {
        var locations = SafetyFileLocations.Sandbox(homeDirectory, projectRoot);
        var notices = LegacySafetyNotice.FromLocations(locations, SafetyFileLocations.SandboxFileName);
        var documents = PermissionsLoader.FilterShadowed(locations).Select(SandboxFileReader.Read).ToList();
        return (EffectiveSandbox.Merge(documents, settings ?? SandboxSettings.Default, trustProject), notices);
    }
}
