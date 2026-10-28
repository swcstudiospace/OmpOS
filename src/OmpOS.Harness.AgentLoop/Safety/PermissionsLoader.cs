namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>Finds, reads and merges the permission files of a user and a workspace.</summary>
public static class PermissionsLoader
{
    /// <summary>Loads the permissions. Files are read each call, so a head reloads after a file changes.</summary>
    /// <param name="homeDirectory">The home directory, or null to skip the user files.</param>
    /// <param name="projectRoot">The workspace root, or null to skip the project files.</param>
    /// <param name="trustProject">False to ignore the allow rules of project files.</param>
    /// <returns>The merged permissions.</returns>
    public static EffectivePermissions Load(string? homeDirectory, string? projectRoot, bool trustProject = true)
    {
        var documents = SafetyFileLocations.Permissions(homeDirectory, projectRoot).Select(PermissionsFileReader.Read).ToList();
        return EffectivePermissions.Merge(documents, trustProject);
    }
}
