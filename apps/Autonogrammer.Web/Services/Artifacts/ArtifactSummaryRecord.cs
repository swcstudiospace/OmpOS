namespace Autonogrammer.Web.Services.Artifacts;

/// <summary>An artifact header as the owner sees it. No content bytes.</summary>
/// <param name="Id">The artifact identifier.</param>
/// <param name="Title">The title.</param>
/// <param name="Kind">The kind name.</param>
/// <param name="Version">The latest version number.</param>
/// <param name="ContentSha256">The latest version hash.</param>
internal sealed record ArtifactSummaryRecord(string Id, string Title, string Kind, int Version, string ContentSha256);
