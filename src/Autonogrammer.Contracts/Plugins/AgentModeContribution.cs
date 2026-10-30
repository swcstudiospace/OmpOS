namespace Autonogrammer.Contracts.Plugins;

/// <summary>
/// An agent mode a plugin contributes to a host. Identifiers are unique among agent-mode contributions.
/// </summary>
public sealed record AgentModeContribution
{
    /// <summary>Gets the identifier of the plugin that contributes this agent mode.</summary>
    public required string PluginId { get; init; }

    /// <summary>Gets the agent-mode identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the title the host shows for this agent mode.</summary>
    public required string Title { get; init; }

    /// <summary>Gets a short description of how the agent behaves in this mode.</summary>
    public required string Description { get; init; }
}
