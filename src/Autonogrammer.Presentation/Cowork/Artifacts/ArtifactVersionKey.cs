namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>The binding of a network grant (D-27): one artifact, one version number, one content hash.</summary>
/// <param name="ArtifactId">The artifact identifier.</param>
/// <param name="Number">The version number.</param>
/// <param name="ContentSha256">The lowercase hex SHA-256 of the version bytes.</param>
public sealed record ArtifactVersionKey(string ArtifactId, int Number, string ContentSha256);
