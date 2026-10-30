namespace Autonogrammer.Cowork.Sessions;

/// <summary>Metadata of one Cowork task that the session record does not store.</summary>
public sealed record CoworkSessionMeta
{
    /// <summary>Gets the project identifier, or null when the task is not in a project.</summary>
    public string? ProjectId { get; init; }

    /// <summary>Gets the trusted folders. Paths stay on this device.</summary>
    public IReadOnlyList<string> TrustedFolders { get; init; } = [];

    /// <summary>Gets whether project memory is off for this task.</summary>
    public bool MemoryOff { get; init; }

    /// <summary>Gets the pinned artifact identifiers.</summary>
    public IReadOnlyList<string> PinnedArtifactIds { get; init; } = [];
}
