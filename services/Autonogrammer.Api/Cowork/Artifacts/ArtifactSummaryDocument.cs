namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>An owner-visible artifact header. No content bytes.</summary>
/// <param name="Id">The artifact identifier.</param>
/// <param name="Title">The title.</param>
/// <param name="Kind">The kind name.</param>
/// <param name="Version">The latest version number.</param>
/// <param name="ContentSha256">The latest hash.</param>
internal sealed record ArtifactSummaryDocument(string Id, string Title, string Kind, int Version, string ContentSha256);
