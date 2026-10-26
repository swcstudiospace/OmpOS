using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using OmpOS.Api.Options;

namespace OmpOS.Api.Security;

/// <summary>
/// Configures the JwtBearer scheme from <see cref="ApiAuthOptions"/>. A bearer token must be issued for the configured audience,
/// carry the optional configured issuer, verify against the authority's signing keys, and carry a subject claim. A token without
/// a subject is rejected, so every authenticated request names its caller.
/// </summary>
internal sealed class JwtBearerSetup : IPostConfigureOptions<JwtBearerOptions>
{
    /// <summary>Gets the claim type that names the authenticated caller.</summary>
    public const string SubjectClaimType = "sub";

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
        options.Events ??= new JwtBearerEvents();
        options.Events.OnTokenValidated = static context =>
        {
            if (context.Principal?.FindFirst(SubjectClaimType) is null)
            {
                context.Fail("The bearer token has no subject claim.");
            }

            return Task.CompletedTask;
        };
    }
}
