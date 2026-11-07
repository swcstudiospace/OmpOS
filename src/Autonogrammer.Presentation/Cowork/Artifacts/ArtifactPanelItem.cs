using Autonogrammer.Contracts.Artifacts;

namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>
/// The header of one version as the panel shows it. It carries no content; the surface receives bytes from the host.
/// </summary>
/// <param name="ArtifactId">The artifact identifier.</param>
/// <param name="Title">The validated title.</param>
/// <param name="Kind">The fixed kind.</param>
/// <param name="Number">The version number.</param>
/// <param name="VersionCount">How many versions the artifact has.</param>
/// <param name="ContentSha256">The lowercase hex SHA-256 of the version bytes.</param>
/// <param name="RequestedOrigins">The origins this version declares. Empty means it asks for no network.</param>
public sealed record ArtifactPanelItem(
    string ArtifactId,
    string Title,
    ArtifactKind Kind,
    int Number,
    int VersionCount,
    string ContentSha256,
    IReadOnlyList<string> RequestedOrigins)
{
    /// <summary>Gets the binding key for grants on this version.</summary>
    public ArtifactVersionKey Key => new(ArtifactId, Number, ContentSha256);
}
