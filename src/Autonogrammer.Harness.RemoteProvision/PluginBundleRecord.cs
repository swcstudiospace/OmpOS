using System.Text.Json.Nodes;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// One plugin of the desktop's bundle record, <c>plugins/bundle.json</c>, written by the release script: the directory, where it came from, the
/// commit it is pinned at, and whether it carries a licence file and runtime dependencies.
/// </summary>
/// <param name="Dir">The plugin directory name.</param>
/// <param name="Source"><c>in-repo</c>, or the https address of the plugin's git repository.</param>
/// <param name="Commit">The 40-character lowercase commit the plugin is pinned at.</param>
/// <param name="LicenseFile">Whether the plugin carries a licence file.</param>
/// <param name="NeedsInstall">Whether the plugin declares runtime dependencies that the release script does not install.</param>
public sealed record PluginBundleRecord(string Dir, string Source, string Commit, bool LicenseFile, bool NeedsInstall)
{
    /// <summary>Returns the record as the bundle record holds it: the same five fields.</summary>
    /// <returns>A new JSON object.</returns>
    public JsonObject ToJson() => new()
    {
        ["dir"] = Dir,
        ["source"] = Source,
        ["commit"] = Commit,
        ["licenseFile"] = LicenseFile,
        ["needsInstall"] = NeedsInstall,
    };
}
