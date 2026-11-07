using Autonogrammer.Api.Services;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Dispatch;

/// <summary>
/// Answers 404 <c>dispatch_disabled</c> for a dispatch route while <c>Dispatch:Enabled</c> is false. Routes stay mapped for OpenAPI.
/// Place it after authorization so an anonymous caller still gets 401.
/// </summary>
internal sealed class DispatchDisabledMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of the <see cref="DispatchDisabledMiddleware"/> class.</summary>
    /// <param name="next">The next middleware.</param>
    public DispatchDisabledMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>Runs the middleware.</summary>
    /// <param name="context">The request context.</param>
    /// <param name="options">The dispatch options.</param>
    /// <returns>A task that completes when the response is written or the next middleware returns.</returns>
    public Task InvokeAsync(HttpContext context, IOptions<DispatchOptions> options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Value.Enabled && context.GetEndpoint()?.Metadata.GetMetadata<DispatchRouteMarker>() is not null)
        {
            return WebProblems.Of(StatusCodes.Status404NotFound, "dispatch_disabled", "Dispatch is disabled.", "Dispatch is not enabled on this server.")
                .ExecuteAsync(context);
        }

        return _next(context);
    }
}
