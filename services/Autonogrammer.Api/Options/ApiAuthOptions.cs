namespace Autonogrammer.Api.Options;

/// <summary>
/// Binds the <c>Api:Auth</c> configuration section: the OIDC bearer authority and audience that every protected request is
/// validated against, the optional issuer, the single-operator owner-token mode, the allow-list of subjects that may use the
/// deployment, and the explicit Development-only opt-in for the single developer principal.
/// </summary>
internal sealed class ApiAuthOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Api:Auth";

    /// <summary>Gets or sets how requests authenticate: OIDC bearer tokens (default) or the single operator's owner token.</summary>
    public ApiAuthMode Mode { get; set; } = ApiAuthMode.Oidc;

    /// <summary>Gets or sets the owner-token settings, used when <see cref="Mode"/> is <see cref="ApiAuthMode.OwnerToken"/>.</summary>
    public ApiOwnerTokenOptions OwnerToken { get; set; } = new();

    /// <summary>
    /// Gets or sets the exact <c>sub</c> values that may use an OIDC deployment. Outside Development an OIDC deployment must set a
    /// non-empty list or <see cref="AllowAnyAuthenticatedUser"/>, because the API runs an agent that can execute commands on the host.
    /// </summary>
    public IList<string> AllowedSubjects { get; set; } = [];

    /// <summary>
    /// Gets or sets whether any holder of a valid OIDC token may use the deployment. Defaults to false. Risky: every account the
    /// identity provider admits can then run an agent that executes commands on the host.
    /// </summary>
    public bool AllowAnyAuthenticatedUser { get; set; }

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
