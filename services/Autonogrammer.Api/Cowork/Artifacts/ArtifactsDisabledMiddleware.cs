using Autonogrammer.Api.Services;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>Answers 404 <c>cowork_disabled</c> for an artifact route while <c>Cowork:Artifacts:Enabled</c> is false.</summary>
internal sealed class ArtifactsDisabledMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of the <see cref="ArtifactsDisabledMiddleware"/> class.</summary>
    /// <param name="next">The next middleware.</param>
    public ArtifactsDisabledMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>Runs the middleware.</summary>
    /// <param name="context">The request.</param>
    /// <param name="options">The artifact API options.</param>
    /// <returns>A task that completes when the response is written or the next middleware returns.</returns>
    public Task InvokeAsync(HttpContext context, IOptions<ArtifactApiOptions> options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Value.Enabled && context.GetEndpoint()?.Metadata.GetMetadata<ArtifactRouteMarker>() is not null)
        {
            return WebProblems.Of(StatusCodes.Status404NotFound, "cowork_disabled", "Cowork artifacts are disabled.", "Cowork artifacts are not enabled on this server.")
                .ExecuteAsync(context);
        }

        return _next(context);
    }
}
