namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>
/// A per-call notice that a legacy safety file was read. Notices are not persisted and this type never writes a file.
/// </summary>
/// <param name="Scope">User or project.</param>
/// <param name="Path">The legacy file that was read.</param>
/// <param name="Enforced">True when the file's rules are in force. False when the current file won and the legacy file was skipped.</param>
/// <param name="Message">The sentence that names the file as a conflict rather than a silent ignore.</param>
public sealed record LegacySafetyNotice(string Scope, string Path, bool Enforced, string Message)
{
    /// <summary>Builds notices for the legacy files in a location list. A legacy file is one whose folder is not the current or Cursor folder.</summary>
    /// <param name="locations">The locations, lowest precedence first.</param>
    /// <param name="fileName">The file name, used only in the sentence.</param>
    /// <returns>One notice per legacy file.</returns>
    public static IReadOnlyList<LegacySafetyNotice> FromLocations(IReadOnlyList<SafetyFileLocation> locations, string fileName)
    {
        ArgumentNullException.ThrowIfNull(locations);
        var currentFolders = new HashSet<string>(StringComparer.Ordinal)
        {
            SafetyFileLocations.AutonogrammerFolder,
            SafetyFileLocations.CursorFolder,
        };
        var notices = new List<LegacySafetyNotice>();
        foreach (var location in locations)
        {
            var folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(location.Path));
            if (folder is null || currentFolders.Contains(folder))
            {
                continue;
            }

            var shadowed = locations.Any(other =>
                other.Scope == location.Scope
                && string.Equals(System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(other.Path)), SafetyFileLocations.AutonogrammerFolder, StringComparison.Ordinal)
                && File.Exists(other.Path));
            var scope = location.Scope == PermissionScope.User ? "user" : "project";
            notices.Add(new LegacySafetyNotice(
                scope,
                location.Path,
                Enforced: !shadowed,
                shadowed
                    ? "Conflict: " + fileName + " in the deprecated directory was not merged because the current file won."
                    : "Conflict: " + fileName + " in the deprecated directory is still enforced. Move it; it is not ignored."));
        }

        return notices;
    }
}
