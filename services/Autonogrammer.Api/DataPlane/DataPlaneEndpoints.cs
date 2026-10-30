using Autonogrammer.Contracts.DataPlane;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Autonogrammer.Api.DataPlane;

/// <summary>The authenticated <c>GET /status/data-plane</c> endpoint: one row per data-plane component with its state and a closed reason.</summary>
internal static class DataPlaneEndpoints
{
    /// <summary>The name of the per-caller rate-limit policy of the endpoint.</summary>
    public const string PolicyName = "data-plane-status";

    /// <summary>Maps the endpoint. The fallback authorization policy applies: an authenticated caller on the allowed-subject list.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapDataPlaneEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/status/data-plane", HandleAsync)
            .WithName("GetDataPlaneStatus")
            .WithTags("Status")
            .WithSummary("Reports the state of each data-plane component: PostgreSQL, Dragonfly, telemetry and the audit ledger.")
            .WithDescription("Each row has the component, its state and a closed reason, and no host, port, version or message. While a store is down every other endpoint that needs it answers 503 with the data_plane_unavailable problem (shown here) and a Retry-After header.")
            .RequireRateLimiting(PolicyName)
            .Produces<DataPlaneStatus[]>(StatusCodes.Status200OK)
            .Produces<DataPlaneProblem>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        return endpoints;
    }

    private static async Task<JsonHttpResult<DataPlaneStatus[]>> HandleAsync(DataPlaneStatusService service, CancellationToken cancellationToken)
    {
        var rows = await service.GetAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Json(rows, DataPlaneJsonContext.Default.DataPlaneStatusArray);
    }
}
