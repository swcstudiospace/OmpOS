namespace Autonogrammer.Contracts.Plugins;

/// <summary>
/// A panel a plugin contributes to a host. Identifiers are unique among panel contributions.
/// </summary>
public sealed record PanelContribution
{
    /// <summary>Gets the identifier of the plugin that contributes this panel.</summary>
    public required string PluginId { get; init; }

    /// <summary>Gets the panel identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the title the host shows for this panel.</summary>
    public required string Title { get; init; }

    /// <summary>Gets a short description of what the panel shows.</summary>
    public required string Description { get; init; }
}
