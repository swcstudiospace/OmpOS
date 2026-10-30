using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.WorkspaceSettings;

/// <summary>One plugin of the workspace plugin inventory.</summary>
public sealed record WorkspacePluginInfo
{
    /// <summary>Gets the plugin identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the name shown to the user.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the plugin's role in a short phrase.</summary>
    public required string Role { get; init; }

    /// <summary>Gets the plugin version, or null when it is not known.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; init; }

    /// <summary>Gets a value indicating whether the plugin was found and healthy.</summary>
    public required bool Found { get; init; }

    /// <summary>Gets a line of detail, or null. It holds no path and no credential.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; init; }
}
