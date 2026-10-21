namespace OmpOS.Contracts.Plugins;

/// <summary>
/// A command a plugin contributes to a host. Identifiers are unique among command contributions.
/// </summary>
public sealed record CommandContribution
{
    /// <summary>Gets the identifier of the plugin that contributes this command.</summary>
    public required string PluginId { get; init; }

    /// <summary>Gets the command identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the title the host shows for this command.</summary>
    public required string Title { get; init; }

    /// <summary>Gets a short description of what the command does.</summary>
    public required string Description { get; init; }
}
