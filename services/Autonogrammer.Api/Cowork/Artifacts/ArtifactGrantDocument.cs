namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>A version-bound grant. No account id.</summary>
/// <param name="ArtifactId">The artifact identifier.</param>
/// <param name="Number">The version number.</param>
/// <param name="ContentSha256">The version hash.</param>
/// <param name="Origins">The origins.</param>
/// <param name="Scope">The scope.</param>
/// <param name="Device">The device.</param>
/// <param name="GrantedAt">When the grant was bound.</param>
internal sealed record ArtifactGrantDocument(string ArtifactId, int Number, string ContentSha256, IReadOnlyList<string> Origins, string Scope, string Device, DateTimeOffset GrantedAt);
