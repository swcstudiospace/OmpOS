namespace OmpOS.AgentWorker.Health;

/// <summary>
/// <c>GET /healthz</c> and <c>GET /readyz</c>: anonymous probes that answer from local state. They emit no telemetry, write no log line
/// and make no outbound call, because Railway counts any outbound packet as activity and would never put the service to sleep.
/// <c>/healthz</c> is also the target of the control plane's wake request.
/// </summary>
internal static class WorkerHealthEndpoints
{
    /// <summary>Gets the liveness route, also the wake target.</summary>
    public const string HealthRoute = "/healthz";

    /// <summary>Gets the readiness route.</summary>
    public const string ReadyRoute = "/readyz";

    /// <summary>Maps the two probes.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapWorkerHealth(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(HealthRoute, static () => Results.Text("ok", "text/plain")).AllowAnonymous();
        endpoints.MapGet(ReadyRoute, static (WorkerReadiness readiness) =>
        {
            var reason = readiness.Check();
            return reason.Length == 0
                ? Results.Text("{\"status\":\"ready\"}", "application/json")
                : Results.Text("{\"status\":\"unready\",\"reason\":\"" + reason + "\"}", "application/json", statusCode: StatusCodes.Status503ServiceUnavailable);
        }).AllowAnonymous();
        return endpoints;
    }
}
