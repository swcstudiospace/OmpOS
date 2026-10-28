using System.Security.Claims;
using Microsoft.Net.Http.Headers;
using Autonogrammer.Api.Security;

namespace Autonogrammer.Api.Middleware;

/// <summary>
/// Development-only authentication boundary. It is registered only when <c>Api:Auth:AllowDevelopmentPrincipal</c> is set, and the
/// options validator refuses that setting outside Development. A request that carries no Authorization header and no validated
/// identity runs as the single developer principal. A request that presents a bearer token keeps its JwtBearer outcome, so an
/// invalid or wrongly audienced token stays 401.
/// </summary>
internal sealed class AuthBoundaryMiddleware
{
    private const string DevelopmentAuthenticationType = "development";

    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of the <see cref="AuthBoundaryMiddleware"/> class.</summary>
    /// <param name="next">The next pipeline step.</param>
    public AuthBoundaryMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Runs a request without a bearer token as the development principal and invokes the pipeline.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Request.Headers.ContainsKey(HeaderNames.Authorization) && context.User.Identity?.IsAuthenticated != true)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.Name, SingleDevPrincipal.DevName)], DevelopmentAuthenticationType));
        }

        await _next(context).ConfigureAwait(false);
    }
}
