namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>A page of artifact headers.</summary>
/// <param name="Artifacts">The headers.</param>
internal sealed record ArtifactListDocument(IReadOnlyList<ArtifactSummaryDocument> Artifacts);
