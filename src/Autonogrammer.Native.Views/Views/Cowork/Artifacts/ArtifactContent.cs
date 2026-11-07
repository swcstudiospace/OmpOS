using Autonogrammer.Contracts.Artifacts;

namespace Autonogrammer.Native.Views.Cowork.Artifacts;

/// <summary>
/// One stored version as the desktop views read it. The host reads the bytes from the store and passes them here; the views
/// never fetch content themselves.
/// </summary>
/// <param name="Kind">The fixed artifact kind. A value outside the defined kinds is shown as metadata with export only.</param>
/// <param name="Title">The validated title.</param>
/// <param name="Bytes">The stored bytes of the version.</param>
/// <param name="Language">The exact language tag of a code artifact, or null.</param>
/// <param name="MediaType">The declared media type of an image, or null.</param>
public sealed record ArtifactContent(
    ArtifactKind Kind,
    string Title,
    ReadOnlyMemory<byte> Bytes,
    string? Language = null,
    string? MediaType = null);
