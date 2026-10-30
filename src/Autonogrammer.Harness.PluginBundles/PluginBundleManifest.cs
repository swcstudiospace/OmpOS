namespace Autonogrammer.Harness.PluginBundles;

/// <summary>The optional <c>.claude-plugin/plugin.json</c> document. Unknown members are rejected.</summary>
public sealed record PluginBundleManifest
{
    /// <summary>Gets the bundle id declared by the manifest, or <see langword="null"/> when the file omits it.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the human description, or <see langword="null"/>.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the version text, or <see langword="null"/>.</summary>
    public string? Version { get; init; }
}
