using Autonogrammer.Contracts.Artifacts;

namespace Autonogrammer.Cowork.Artifacts.Model;

/// <summary>
/// The artifact header. Kind is fixed at creation. <paramref name="SessionId"/> is an optional link that is not a
/// foreign key, so it survives deletion of the session.
/// </summary>
/// <param name="Id">Host-generated version-7 identifier.</param>
/// <param name="Kind">Fixed kind.</param>
/// <param name="Title">Validated title.</param>
/// <param name="MediaType">Derived media type, or the accepted image type.</param>
/// <param name="Language">Language tag for code, otherwise null.</param>
/// <param name="SessionId">Optional session link.</param>
/// <param name="DerivedFrom">Set only on a copy.</param>
public sealed record ArtifactRecord(
    string Id,
    ArtifactKind Kind,
    string Title,
    string MediaType,
    string? Language,
    string? SessionId,
    ArtifactDerivation? DerivedFrom);
