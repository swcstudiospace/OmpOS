namespace Autonogrammer.Cowork.Artifacts.Model;

/// <summary>A durable grant of network access bound to one artifact version. Session grants never use this type: they are memory-only by design and must not be persisted.</summary>
/// <param name="ArtifactId">The artifact identifier.</param>
/// <param name="Number">The version number the grant covers.</param>
/// <param name="ContentSha256">The version hash the grant was bound to.</param>
/// <param name="Origins">The origins this grant allows. A subset of the version's requested origins, never a superset.</param>
/// <param name="Scope">The grant scope, for example <c>session</c> or <c>version</c>.</param>
/// <param name="Device">The device the grant names.</param>
/// <param name="GrantedAt">When the grant was bound.</param>
public sealed record ArtifactGrantBinding(
    string ArtifactId,
    int Number,
    string ContentSha256,
    IReadOnlyList<string> Origins,
    string Scope,
    string Device,
    DateTimeOffset GrantedAt);
