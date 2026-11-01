namespace Autonogrammer.Harness.PluginBundles;

/// <summary>The <c>plugins-state.json</c> document.</summary>
internal sealed class BundleStateDocument
{
    /// <summary>Gets or sets the bundle flags, keyed by bundle id.</summary>
    public Dictionary<string, BundleStateEntry> Bundles { get; set; } = new(StringComparer.Ordinal);
}
