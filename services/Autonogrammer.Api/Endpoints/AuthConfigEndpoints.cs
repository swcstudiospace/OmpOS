using Autonogrammer.Api.Options;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Endpoints;

/// <summary>
/// The anonymous <c>GET /auth/config</c> endpoint: tells a client how this deployment authenticates (<c>owner-token</c>, <c>oidc</c> or
/// <c>development</c>). The answer carries no secret and is never cached. It has its own per-client-address rate-limit policy.
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
        context.Response.Headers.CacheControl = "no-store";
        if (auth.Mode == ApiAuthMode.OwnerToken)
        {
            return TypedResults.Json(new AuthConfigResponse("owner-token", null, null), ApiJsonContext.Default.AuthConfigResponse);
        }

        var response = auth.AllowDevelopmentPrincipal
            ? new AuthConfigResponse("development", null, null)
            : new AuthConfigResponse("oidc", Blank(auth.Authority), Blank(auth.Audience));
        return TypedResults.Json(response, ApiJsonContext.Default.AuthConfigResponse);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
