using Microsoft.Extensions.Options;
using OmpOS.Api.Services;

namespace OmpOS.Api.Automations;

/// <summary>
/// Answers 404 <c>automations_disabled</c> for an automation route while <c>Automations:Enabled</c> is false. The routes are always mapped (the OpenAPI document describes them),
/// and this runs before the endpoint, so no handler or automation service is resolved while automations are off. Place it after authorization: an anonymous caller of a management route still gets 401.
/// </summary>
internal sealed class AutomationDisabledMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of the <see cref="AutomationDisabledMiddleware"/> class.</summary>
    /// <param name="next">The next middleware.</param>
    public AutomationDisabledMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>Runs the middleware.</summary>
    /// <param name="context">The request context.</param>
    /// <param name="options">The automation options.</param>
    /// <returns>A task that completes when the response is written or the next middleware returns.</returns>
    public Task InvokeAsync(HttpContext context, IOptions<AutomationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Value.Enabled && context.GetEndpoint()?.Metadata.GetMetadata<AutomationRouteMarker>() is not null)
        {
            return WebProblems.Of(StatusCodes.Status404NotFound, "automations_disabled", "Automations are disabled.", "Automations are not enabled on this server.")
                .ExecuteAsync(context);
        }

        return _next(context);
    }
}
