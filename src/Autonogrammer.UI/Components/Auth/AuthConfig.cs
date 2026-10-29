namespace Autonogrammer.UI.Components.Auth;

/// <summary>
/// The answer of the anonymous <c>GET /auth/config</c>: how a client authenticates against this deployment. It never carries a secret.
/// </summary>
/// <param name="Mode"><see cref="OwnerTokenMode"/>, <see cref="OidcMode"/> or <see cref="DevelopmentMode"/>.</param>
/// <param name="Authority">The OIDC authority, or null when the mode has none.</param>
/// <param name="Audience">The OIDC audience, or null when the mode has none.</param>
public sealed record AuthConfig(string Mode, string? Authority, string? Audience)
{
    /// <summary>The deployment has one operator: the client sends <c>Authorization: Bearer</c> with the owner token.</summary>
    public const string OwnerTokenMode = "owner-token";

    /// <summary>The deployment needs an OIDC sign-in.</summary>
    public const string OidcMode = "oidc";

    /// <summary>The deployment needs no credential (a local development API).</summary>
    public const string DevelopmentMode = "development";

    /// <summary>Gets whether <see cref="Mode"/> is one of the three modes this client understands.</summary>
    /// <returns>True for <see cref="OwnerTokenMode"/>, <see cref="OidcMode"/> and <see cref="DevelopmentMode"/>.</returns>
    public bool IsKnownMode() =>
        string.Equals(Mode, OwnerTokenMode, StringComparison.Ordinal)
        || string.Equals(Mode, OidcMode, StringComparison.Ordinal)
        || string.Equals(Mode, DevelopmentMode, StringComparison.Ordinal);
}
