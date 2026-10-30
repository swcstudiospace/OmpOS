namespace Autonogrammer.Contracts.Plugins;

/// <summary>
/// A tool a plugin contributes to a host for agents to invoke. Identifiers are unique among tool contributions.
/// </summary>
public sealed record ToolContribution
{
    /// <summary>Gets the identifier of the plugin that contributes this tool.</summary>
    public required string PluginId { get; init; }

    /// <summary>Gets the tool identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the title the host shows for this tool.</summary>
    public required string Title { get; init; }

    /// <summary>Gets a short description of what the tool does.</summary>
    public required string Description { get; init; }
}
