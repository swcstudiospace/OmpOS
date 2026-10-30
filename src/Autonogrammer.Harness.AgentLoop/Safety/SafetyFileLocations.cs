using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>The files Autonogrammer reads, from the lowest precedence to the highest: the user's Cursor file, the user's Autonogrammer file, the project's Cursor file, the project's Autonogrammer file.</summary>
public static class SafetyFileLocations
{
    /// <summary>The name of the permissions file.</summary>
    public const string PermissionsFileName = "permissions.json";

    /// <summary>The name of the sandbox file.</summary>
    public const string SandboxFileName = "sandbox.json";

    /// <summary>The Autonogrammer folder.</summary>
    public const string AutonogrammerFolder = ".autonogrammer";

    /// <summary>The Cursor folder.</summary>
    public const string CursorFolder = ".cursor";

    /// <summary>Lists the places a permissions file may be.</summary>
    /// <param name="homeDirectory">The home directory, or null to skip the user files.</param>
    /// <param name="projectRoot">The workspace root, or null to skip the project files.</param>
    /// <returns>The locations, lowest precedence first.</returns>
    public static IReadOnlyList<SafetyFileLocation> Permissions(string? homeDirectory, string? projectRoot) => For(PermissionsFileName, homeDirectory, projectRoot);

    /// <summary>Lists the places a sandbox file may be.</summary>
    /// <param name="homeDirectory">The home directory, or null to skip the user files.</param>
    /// <param name="projectRoot">The workspace root, or null to skip the project files.</param>
    /// <returns>The locations, lowest precedence first.</returns>
    public static IReadOnlyList<SafetyFileLocation> Sandbox(string? homeDirectory, string? projectRoot) => For(SandboxFileName, homeDirectory, projectRoot);

    private static List<SafetyFileLocation> For(string fileName, string? homeDirectory, string? projectRoot)
    {
        var locations = new List<SafetyFileLocation>(6);
        AddScope(locations, homeDirectory, PermissionScope.User, fileName);
        AddScope(locations, projectRoot, PermissionScope.Project, fileName);
        return locations;
    }

    private static void AddScope(List<SafetyFileLocation> locations, string? root, PermissionScope scope, string fileName)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        locations.Add(new SafetyFileLocation(Path.Combine(root, CursorFolder, fileName), scope, PermissionFileOrigin.Cursor));
        var legacy = Path.Combine(root, LegacyConventionDiscovery.LegacyDirectoryName, fileName);
        if (File.Exists(legacy))
        {
            locations.Add(new SafetyFileLocation(legacy, scope, PermissionFileOrigin.Autonogrammer));
        }

        locations.Add(new SafetyFileLocation(Path.Combine(root, AutonogrammerFolder, fileName), scope, PermissionFileOrigin.Autonogrammer));
    }
}
