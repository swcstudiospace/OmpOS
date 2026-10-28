namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>The files OmpOS reads, from the lowest precedence to the highest: the user's Cursor file, the user's OmpOS file, the project's Cursor file, the project's OmpOS file.</summary>
public static class SafetyFileLocations
{
    /// <summary>The name of the permissions file.</summary>
    public const string PermissionsFileName = "permissions.json";

    /// <summary>The name of the sandbox file.</summary>
    public const string SandboxFileName = "sandbox.json";

    /// <summary>The OmpOS folder.</summary>
    public const string OmposFolder = ".ompos";

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
        var locations = new List<SafetyFileLocation>(4);
        if (!string.IsNullOrWhiteSpace(homeDirectory))
        {
            locations.Add(new SafetyFileLocation(Path.Combine(homeDirectory, CursorFolder, fileName), PermissionScope.User, PermissionFileOrigin.Cursor));
            locations.Add(new SafetyFileLocation(Path.Combine(homeDirectory, OmposFolder, fileName), PermissionScope.User, PermissionFileOrigin.Ompos));
        }

        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            locations.Add(new SafetyFileLocation(Path.Combine(projectRoot, CursorFolder, fileName), PermissionScope.Project, PermissionFileOrigin.Cursor));
            locations.Add(new SafetyFileLocation(Path.Combine(projectRoot, OmposFolder, fileName), PermissionScope.Project, PermissionFileOrigin.Ompos));
        }

        return locations;
    }
}
