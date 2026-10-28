namespace Autonogrammer.Api.Options;

/// <summary>
/// Binds the <c>Api:Auth</c> configuration section: the OIDC bearer authority and audience that every protected request is
/// validated against, the optional issuer, and the explicit Development-only opt-in for the single developer principal.
/// </summary>
internal sealed class ApiAuthOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Api:Auth";

    /// <summary>Gets or sets the OIDC authority, an absolute https URI whose metadata verifies bearer tokens. Required outside Development.</summary>
    public string? Authority { get; set; }

    /// <summary>Gets or sets the audience a bearer token must be issued for. Required outside Development.</summary>
    public string? Audience { get; set; }

    /// <summary>Gets or sets the issuer a bearer token must carry. Optional; unset means the issuer from the authority metadata applies.</summary>
    public string? ValidIssuer { get; set; }

    /// <summary>
    /// Gets or sets whether requests without a validated token run as the single developer principal. Defaults to false and is
    /// refused outside the Development environment.
    /// </summary>
    public bool AllowDevelopmentPrincipal { get; set; }
}
