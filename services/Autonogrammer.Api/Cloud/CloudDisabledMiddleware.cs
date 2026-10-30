using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Api.Services;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// Answers 404 <c>cloud_disabled</c> for a cloud route while <c>Cloud:Enabled</c> is false. The routes are always mapped (the OpenAPI document describes them), and this
/// runs before the endpoint, so no handler or control-plane service is resolved while cloud is off. Place it after authorization: an anonymous caller still gets 401.
/// </summary>
internal sealed class CloudDisabledMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of the <see cref="CloudDisabledMiddleware"/> class.</summary>
    /// <param name="next">The next middleware.</param>
    public CloudDisabledMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>Runs the middleware.</summary>
    /// <param name="context">The request context.</param>
    /// <param name="options">The cloud options.</param>
    /// <returns>A task that completes when the response is written or the next middleware returns.</returns>
    public Task InvokeAsync(HttpContext context, IOptions<CloudOptions> options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Value.Enabled && context.GetEndpoint()?.Metadata.GetMetadata<CloudRouteMarker>() is not null)
        {
            return WebProblems.Of(StatusCodes.Status404NotFound, "cloud_disabled", "Cloud agents are disabled.", "Cloud agents are not enabled on this server.")
                .ExecuteAsync(context);
        }

        return _next(context);
    }
}
