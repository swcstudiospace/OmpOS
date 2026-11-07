using Autonogrammer.Contracts.Accounts;

namespace Autonogrammer.UI.Components.Auth;

/// <summary>
/// The answer of the anonymous <c>GET /auth/config</c>: how a client authenticates against this deployment. It never carries a secret.
/// </summary>
/// <param name="Mode"><see cref="OwnerTokenMode"/>, <see cref="OidcMode"/> or <see cref="DevelopmentMode"/>.</param>
/// <param name="Authority">The OIDC authority, or null when the mode has none.</param>
/// <param name="Audience">The OIDC audience, or null when the mode has none.</param>
/// <param name="ProjectUrl">The https project URL of the auth host, or null when no client block is configured.</param>
/// <param name="PublishableKey">The publishable key, or null when no client block is configured. It is public by design.</param>
/// <param name="Methods">The offered sign-in methods, or null when no client block is configured.</param>
/// <param name="EmailCode">The email code timing, or null when no client block is configured.</param>
/// <param name="SignupEmailHasCode">True when a first-time email already shows a code, or null when no client block is configured.</param>
public sealed record AuthConfig(
    string Mode,
    string? Authority,
    string? Audience,
    Uri? ProjectUrl = null,
    string? PublishableKey = null,
    AuthMethods? Methods = null,
    AuthEmailCode? EmailCode = null,
    bool? SignupEmailHasCode = null)
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

    /// <summary>Gets whether the API served an OIDC client block. Owner-token and development answers omit it.</summary>
    public bool HasClientBlock =>
        ProjectUrl is not null || !string.IsNullOrEmpty(PublishableKey) || Methods is not null || EmailCode is not null;
}
