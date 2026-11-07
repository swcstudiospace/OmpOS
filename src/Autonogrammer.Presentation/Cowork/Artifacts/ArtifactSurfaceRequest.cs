using Autonogrammer.Contracts.Artifacts;

namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>
/// The identity of one version to show and the origins approved for it. Bytes never travel in this record.
/// </summary>
/// <param name="ArtifactId">The artifact identifier.</param>
/// <param name="Number">The version number.</param>
/// <param name="ContentSha256">The lowercase hex SHA-256 of the version bytes.</param>
/// <param name="Kind">The fixed artifact kind.</param>
/// <param name="ApprovedOrigins">The origins the user approved for this version. Empty means no network.</param>
public sealed record ArtifactSurfaceRequest(
    string ArtifactId,
    int Number,
    string ContentSha256,
    ArtifactKind Kind,
    IReadOnlyList<string> ApprovedOrigins);
