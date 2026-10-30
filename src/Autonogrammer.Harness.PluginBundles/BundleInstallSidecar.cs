namespace Autonogrammer.Harness.PluginBundles;

/// <summary>The sidecar written inside an installed bundle. It records scope and is not user content.</summary>
internal sealed class BundleInstallSidecar
{
    /// <summary>Gets or sets the bundle id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets <c>user</c> or <c>project</c>.</summary>
    public string Scope { get; set; } = "user";
}
