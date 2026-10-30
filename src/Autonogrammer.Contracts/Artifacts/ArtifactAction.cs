namespace Autonogrammer.Contracts.Artifacts;

/// <summary>
/// Why an artifact card was emitted. The card opens the panel and never carries content inline.
/// </summary>
public enum ArtifactAction
{
    /// <summary>The artifact was created.</summary>
    Created,

    /// <summary>The artifact gained a new version.</summary>
    Updated,
}
