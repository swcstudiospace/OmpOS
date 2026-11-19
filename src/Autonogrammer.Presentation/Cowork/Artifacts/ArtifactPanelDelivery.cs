namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>
/// One delivered version: its header and the bytes the host read from the store. The panel keeps it, so a view bound after the delivery
/// shows the same content and the surface loads the same document.
/// </summary>
/// <param name="Item">The version header.</param>
/// <param name="Bytes">The stored bytes of the version.</param>
/// <param name="Language">The exact language tag of a code version, or null.</param>
/// <param name="MediaType">The declared media type of an image version, or null.</param>
public sealed record ArtifactPanelDelivery(ArtifactPanelItem Item, ReadOnlyMemory<byte> Bytes, string? Language, string? MediaType);
