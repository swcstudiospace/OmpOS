namespace Autonogrammer.Cowork.Artifacts.Model;

/// <summary>Points a copy at the source artifact, version and content hash.</summary>
/// <param name="ArtifactId">The source artifact identifier.</param>
/// <param name="Version">The source version number.</param>
/// <param name="ContentSha256">The lowercase hex SHA-256 of the source bytes.</param>
public sealed record ArtifactDerivation(string ArtifactId, int Version, string ContentSha256);
