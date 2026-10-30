#pragma warning disable MA0048 // The plan owns this file for the middleware and its route markers.
using Autonogrammer.Api.Cowork.Memory;
using Autonogrammer.Api.Cowork.Projects;
using Autonogrammer.Api.Services;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cowork;

/// <summary>Marks a project or global-instruction route so <see cref="CoworkDisabledMiddleware"/> can answer while that module is off.</summary>
internal sealed class CoworkProjectsRouteMarker
{
    private CoworkProjectsRouteMarker()
    {
    }

    /// <summary>Gets the shared marker.</summary>
    public static CoworkProjectsRouteMarker Instance { get; } = new();
}

/// <summary>Marks a memory route so <see cref="CoworkDisabledMiddleware"/> can answer while that module is off.</summary>
internal sealed class CoworkMemoryRouteMarker
{
    private CoworkMemoryRouteMarker()
    {
    }

    /// <summary>Gets the shared marker.</summary>
    public static CoworkMemoryRouteMarker Instance { get; } = new();
}

/// <summary>
/// Answers 404 <c>cowork_disabled</c> for a Cowork module that is off. Project routes and <c>/cowork/instructions/global</c> follow
/// <c>Cowork:Projects:Enabled</c>. Memory routes follow <c>Cowork:Memory:Enabled</c>. Place it after authorization so an anonymous caller still gets 401.
/// </summary>
internal sealed class CoworkDisabledMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of the <see cref="CoworkDisabledMiddleware"/> class.</summary>
    /// <param name="next">The next middleware.</param>
    public CoworkDisabledMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>Runs the middleware.</summary>
    /// <param name="context">The request context.</param>
    /// <param name="projects">The project options.</param>
    /// <param name="memory">The memory options.</param>
    /// <returns>A task that completes when the response is written or the next middleware returns.</returns>
    public Task InvokeAsync(HttpContext context, IOptions<ProjectsOptions> projects, IOptions<MemoryOptions> memory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(memory);
        var path = context.Request.Path.Value ?? string.Empty;
        var endpoint = context.GetEndpoint();
        var projectsRoute = endpoint?.Metadata.GetMetadata<CoworkProjectsRouteMarker>() is not null
            || path.StartsWith("/cowork/projects", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/cowork/instructions/global", StringComparison.OrdinalIgnoreCase);
        var memoryRoute = endpoint?.Metadata.GetMetadata<CoworkMemoryRouteMarker>() is not null
            || path.StartsWith("/cowork/memory", StringComparison.OrdinalIgnoreCase);
        if (projectsRoute && !projects.Value.Enabled)
        {
            return DisabledAsync(context, "Project sync is not enabled on this server.");
        }

        if (memoryRoute && !memory.Value.Enabled)
        {
            return DisabledAsync(context, "Memory is not enabled on this server.");
        }

        return _next(context);
    }

    private static Task DisabledAsync(HttpContext context, string detail) =>
        WebProblems.Of(StatusCodes.Status404NotFound, "cowork_disabled", "Cowork module disabled.", detail).ExecuteAsync(context);
}
