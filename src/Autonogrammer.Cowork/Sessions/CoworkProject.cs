namespace Autonogrammer.Cowork.Sessions;

/// <summary>A local Cowork project. Sharing and scheduling are not fields of this record.</summary>
public sealed record CoworkProject
{
    /// <summary>Gets the project identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the project name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the project folder on this device, or null when it has none.</summary>
    public string? Folder { get; init; }

    /// <summary>Gets the project instructions, or null when it has none.</summary>
    public string? Instructions { get; init; }
}
