using System.Text.Json.Serialization;

namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>One entry of a workspace folder listing.</summary>
public sealed record WorkspaceTreeEntry
{
    /// <summary>Gets the entry name, without its folder.</summary>
    public required string Name { get; init; }

    /// <summary>Gets whether the entry is a file or a folder.</summary>
    public required WorkspaceEntryKind Kind { get; init; }

    /// <summary>Gets the size of a file in bytes, or null for a folder.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Size { get; init; }
}
