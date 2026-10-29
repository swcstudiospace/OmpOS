using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Autonogrammer.Api.Hosting;

/// <summary>Endpoints owned by the hosting layer.</summary>
internal static class HostingEndpointExtensions
{
    /// <summary>Maps the anonymous <c>/health/startup</c> probe: 503 until the startup gate opens, then 200.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapHostingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapHealthChecks("/health/startup", new HealthCheckOptions
        {
            Predicate = static check => check.Tags.Contains(HostingPolicies.StartupTag),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            },
        }).AllowAnonymous();
        return endpoints;
    }
}
