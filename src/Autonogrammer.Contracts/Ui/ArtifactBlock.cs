using Autonogrammer.Contracts.Artifacts;

namespace Autonogrammer.Contracts.Ui;

/// <summary>
/// A card that opens the artifact panel (kind "artifact"). Content is never inlined.
/// </summary>
public sealed record ArtifactBlock : UiBlock
{
    /// <summary>Gets the host-generated artifact identifier.</summary>
    public required string ArtifactId { get; init; }

    /// <summary>Gets the version number the card refers to.</summary>
    public required int Version { get; init; }

    /// <summary>Gets the display title. Validation lives in the core library, not in this contract.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the fixed artifact kind.</summary>
    public required ArtifactKind ArtifactKind { get; init; }

    /// <summary>Gets whether the card reports a create or an update.</summary>
    public required ArtifactAction Action { get; init; }
}
