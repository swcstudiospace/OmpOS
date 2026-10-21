using System.Security.Claims;
using OmpOS.Api.Security;

namespace OmpOS.Api.Middleware;

/// <summary>
/// No-op authentication boundary: publishes the ambient <see cref="IUserContext"/> onto the
/// request (items plus a named <see cref="ClaimsPrincipal"/>) so endpoints never branch on
/// authentication mechanics. A real deployment replaces the <c>IUserContext</c> registration;
/// this middleware stays the single place that decision lands.
/// </summary>
internal sealed class AuthBoundaryMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of the <see cref="AuthBoundaryMiddleware"/> class.</summary>
    /// <param name="next">The next pipeline step.</param>
    public AuthBoundaryMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Publishes the ambient principal and invokes the pipeline.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="user">The ambient user context.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task InvokeAsync(HttpContext context, IUserContext user)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(user);
        context.Items[typeof(IUserContext)] = user;
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Name, user.Name)], "dev"));
        await _next(context).ConfigureAwait(false);
    }
}
