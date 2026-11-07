using Autonogrammer.Api.Options;
using Autonogrammer.Contracts.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Endpoints;

/// <summary>
/// The anonymous <c>GET /auth/config</c> endpoint: tells a client how this deployment authenticates (<c>owner-token</c>, <c>oidc</c> or
/// <c>development</c>) and, in OIDC mode with a client block, the public sign-in settings. It has its own per-client-address rate-limit policy.
/// </summary>
internal static class AuthConfigEndpoints
{
    /// <summary>The name of the rate-limit policy of the endpoint.</summary>
    public const string PolicyName = "auth-config";

    /// <summary>Maps the endpoint.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/auth/config", Handle)
            .WithName("GetAuthConfig")
            .WithTags("Auth")
            .WithSummary("Describes how clients authenticate to this deployment. Anonymous; carries no secret.")
            .AllowAnonymous()
            .RequireRateLimiting(PolicyName)
            .Produces<AuthConfigResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        return endpoints;
    }

    private static JsonHttpResult<AuthConfigResponse> Handle(HttpContext context, IOptions<ApiAuthOptions> options)
    {
        ArgumentNullException.ThrowIfNull(context);
        var auth = options.Value;
        var includeClient = auth.Mode == ApiAuthMode.Oidc && !auth.AllowDevelopmentPrincipal && auth.Client.HasAny;
        context.Response.Headers.CacheControl = includeClient ? "public, max-age=300" : "no-store";
        AuthConfigResponse response;
        if (auth.Mode == ApiAuthMode.OwnerToken)
        {
            response = new AuthConfigResponse("owner-token", null, null);
        }
        else if (auth.AllowDevelopmentPrincipal)
        {
            response = new AuthConfigResponse("development", null, null);
        }
        else if (!includeClient)
        {
            response = new AuthConfigResponse("oidc", Blank(auth.Authority), Blank(auth.Audience));
        }
        else
        {
            var client = auth.Client;
            Uri? projectUrl = Uri.TryCreate(client.ProjectUrl, UriKind.Absolute, out var parsed) ? parsed : null;
            response = new AuthConfigResponse(
                "oidc",
                Blank(auth.Authority),
                Blank(auth.Audience),
                projectUrl,
                Blank(client.PublishableKey),
                new AuthMethods(client.EmailCodeEnabled, client.MagicLinkEnabled, client.Providers.Distinct(StringComparer.Ordinal).ToArray(), client.WalletEnabled),
                new AuthEmailCode(client.EmailOtpExpirySeconds, client.ResendCooldownSeconds),
                client.SignupEmailHasCode);
        }

        return TypedResults.Json(response, AccountsJsonContext.Default.AuthConfigResponse);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
