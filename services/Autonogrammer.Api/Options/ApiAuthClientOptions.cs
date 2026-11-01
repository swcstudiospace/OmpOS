namespace Autonogrammer.Api.Options;

/// <summary>
/// Binds the <c>Api:Auth:Client</c> configuration block: the public sign-in settings of the shared identity project that the API
/// serves to anonymous callers so a head can start a sign-in. Everything here is public by design, so a secret key or a
/// service-role token must never be configured; <see cref="ApiAuthOptionsValidator"/> refuses both at start. The block is valid in
/// OIDC mode only.
/// </summary>
internal sealed class ApiAuthClientOptions
{
    /// <summary>Gets the configuration section name relative to the application root.</summary>
    public const string SectionName = "Api:Auth:Client";

    /// <summary>Gets or sets the identity project URL, an absolute https URI without user information.</summary>
    public string? ProjectUrl { get; set; }

    /// <summary>Gets or sets the publishable (public) project key. A secret key or a service-role token is refused.</summary>
    public string? PublishableKey { get; set; }

    /// <summary>Gets or sets whether the sign-in link method is offered. Requires <see cref="EmailCodeEnabled"/>.</summary>
    public bool MagicLinkEnabled { get; set; }

    /// <summary>Gets or sets whether the one-time code method is offered.</summary>
    public bool EmailCodeEnabled { get; set; } = true;

    /// <summary>Gets or sets how long a one-time code stays valid, in seconds.</summary>
    public int EmailOtpExpirySeconds { get; set; } = 3600;

    /// <summary>Gets or sets the shortest wait before a new code may be requested, in seconds.</summary>
    public int ResendCooldownSeconds { get; set; } = 60;

    /// <summary>Gets or sets whether the identity project's sign-up email carries a code rather than a link.</summary>
    public bool SignupEmailHasCode { get; set; }

    /// <summary>Gets a value indicating whether a project URL or a publishable key is set, which marks the block as in use.</summary>
    public bool HasAny => !string.IsNullOrWhiteSpace(ProjectUrl) || !string.IsNullOrWhiteSpace(PublishableKey);
}
