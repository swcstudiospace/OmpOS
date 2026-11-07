using Autonogrammer.Api.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Security;

/// <summary>
/// Configures the JwtBearer scheme from <see cref="ApiAuthOptions"/>. A bearer token must be signed (only the configured algorithms,
/// <c>ES256</c> by default), be issued for the configured audience and issuer, be inside its lifetime (30 s of clock skew), carry a
/// subject claim, carry the role <c>authenticated</c> and not be anonymous. A token without a subject, an anonymous token (the
/// identity provider's <c>is_anonymous</c> claim) and a token whose role is missing or anything else are rejected, so every
/// authenticated request names a signed-in caller.
/// </summary>
internal sealed class JwtBearerSetup : IPostConfigureOptions<JwtBearerOptions>
{
    /// <summary>Gets the claim type that names the authenticated caller.</summary>
    public const string SubjectClaimType = "sub";

    /// <summary>Gets the claim type that carries the identity provider's role.</summary>
    public const string RoleClaimType = "role";

    /// <summary>Gets the only role a bearer token may carry.</summary>
    public const string AuthenticatedRole = "authenticated";

    /// <summary>Gets the claim type that marks an anonymous sign-in.</summary>
    public const string AnonymousClaimType = "is_anonymous";

    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    private readonly IOptions<ApiAuthOptions> _auth;

    /// <summary>Initializes a new instance of the <see cref="JwtBearerSetup"/> class.</summary>
    /// <param name="auth">The validated authentication options.</param>
    public JwtBearerSetup(IOptions<ApiAuthOptions> auth)
    {
        _auth = auth;
    }

    /// <inheritdoc />
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var auth = _auth.Value;
        options.Authority = string.IsNullOrWhiteSpace(auth.Authority) ? null : auth.Authority;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.ValidAudience = auth.Audience;
        options.TokenValidationParameters.ValidIssuer = string.IsNullOrWhiteSpace(auth.ValidIssuer) ? null : auth.ValidIssuer;
        options.TokenValidationParameters.ValidAlgorithms = auth.EffectiveAlgorithms;
        options.TokenValidationParameters.RequireSignedTokens = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ClockSkew = ClockSkew;
        options.Events ??= new JwtBearerEvents();
        options.Events.OnTokenValidated = static context =>
        {
            var principal = context.Principal;
            if (principal?.FindFirst(SubjectClaimType) is null)
            {
                context.Fail("The bearer token has no subject claim.");
            }
            else if (string.Equals(principal.FindFirst(AnonymousClaimType)?.Value, "true", StringComparison.OrdinalIgnoreCase))
            {
                context.Fail("The bearer token is anonymous.");
            }
            else if (!string.Equals(principal.FindFirst(RoleClaimType)?.Value, AuthenticatedRole, StringComparison.Ordinal))
            {
                context.Fail("The bearer token does not carry the authenticated role.");
            }

            return Task.CompletedTask;
        };
    }
}
