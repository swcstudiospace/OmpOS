namespace Autonogrammer.Web.Services.Artifacts;

/// <summary>The artifacts of the caller.</summary>
/// <param name="Artifacts">The headers.</param>
internal sealed record ArtifactListRecord(List<ArtifactSummaryRecord> Artifacts);
