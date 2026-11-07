using Autonogrammer.Contracts.Artifacts;

namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>One artifact row in the panel list.</summary>
/// <param name="ArtifactId">The artifact identifier.</param>
/// <param name="Title">The validated title.</param>
/// <param name="Kind">The fixed kind.</param>
/// <param name="VersionCount">How many versions the artifact has.</param>
/// <param name="RequestedOrigins">The origins the newest version declares.</param>
public sealed record ArtifactListItem(
    string ArtifactId,
    string Title,
    ArtifactKind Kind,
    int VersionCount,
    IReadOnlyList<string> RequestedOrigins);
