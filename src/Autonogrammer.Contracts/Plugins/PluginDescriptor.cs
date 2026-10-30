namespace Autonogrammer.Contracts.Plugins;

/// <summary>
/// Identifies a plugin that contributes commands, panels, tools and agent modes to a host.
/// Carries data only; each host decides how to present its contributions.
/// </summary>
public sealed record PluginDescriptor
{
    /// <summary>Gets the stable plugin identifier that every contribution references.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the human-readable plugin name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the plugin version string.</summary>
    public required string Version { get; init; }
}
