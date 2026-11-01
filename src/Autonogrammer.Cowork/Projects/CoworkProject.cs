namespace Autonogrammer.Cowork.Projects;

/// <summary>
/// A Cowork project on this device. <see cref="Folders"/> never leave the device.
/// </summary>
public sealed record CoworkProject
{
    /// <summary>Gets the project identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the project name. One to 100 characters.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the description, or null when it has none.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the project instructions, or null when it has none. At most 16 KiB.</summary>
    public string? Instructions { get; init; }

    /// <summary>Gets the https context links. At most 20.</summary>
    public IReadOnlyList<string> ContextUrls { get; init; } = [];

    /// <summary>Gets the local folders. The first entry is the legacy <c>folder</c> value. Never uploaded.</summary>
    public IReadOnlyList<string> Folders { get; init; } = [];

    /// <summary>Gets whether account memory is used. The default is false.</summary>
    public bool UseAccountMemory { get; init; }

    /// <summary>Gets when the project was archived, or null when it is active.</summary>
    public DateTimeOffset? ArchivedAt { get; init; }

    /// <summary>Gets when the project was last updated.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}
