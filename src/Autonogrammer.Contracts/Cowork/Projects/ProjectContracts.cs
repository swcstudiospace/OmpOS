#pragma warning disable MA0048 // The plan owns this file for the account-sync records.
namespace Autonogrammer.Contracts.Cowork.Projects;

/// <summary>
/// Account-sync metadata for one project. Folders are not a member: they stay on the device.
/// </summary>
public sealed record ProjectSyncRecord
{
    /// <summary>Gets the project identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the project name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the project instructions.</summary>
    public string? Instructions { get; init; }

    /// <summary>Gets the https context links.</summary>
    public IReadOnlyList<string> ContextUrls { get; init; } = [];

    /// <summary>Gets whether account memory is used.</summary>
    public bool UseAccountMemory { get; init; }

    /// <summary>Gets when the project was archived.</summary>
    public DateTimeOffset? ArchivedAt { get; init; }

    /// <summary>Gets when the project was last updated.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>The account-global instructions document. At most 8 KiB.</summary>
public sealed record GlobalInstructionsRecord
{
    /// <summary>Gets the instructions text.</summary>
    public string? Instructions { get; init; }
}
