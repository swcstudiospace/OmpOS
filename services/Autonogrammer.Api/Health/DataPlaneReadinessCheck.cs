using Autonogrammer.Api.DataPlane;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Autonogrammer.Api.Health;

/// <summary>
/// Readiness of the PostgreSQL schema: unhealthy while the schema is behind the compiled migration head (D-58), healthy when it is current or ahead and for a
/// session store that is not PostgreSQL. Whether PostgreSQL and Dragonfly are reachable is answered by the <c>repository</c> and <c>coordination</c> checks, which
/// already carry the <c>ready</c> tag. The description names a state, never a connection detail.
/// </summary>
internal sealed class DataPlaneReadinessCheck : IHealthCheck
{
    private readonly SchemaHeadProbe _schema;

    /// <summary>Initializes a new instance of the <see cref="DataPlaneReadinessCheck"/> class.</summary>
    /// <param name="schema">The schema probe.</param>
    public DataPlaneReadinessCheck(SchemaHeadProbe schema)
    {
        _schema = schema;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _schema.GetAsync(refresh: false, cancellationToken).ConfigureAwait(false) switch
            {
                SchemaState.Behind => HealthCheckResult.Unhealthy("The schema is behind the compiled migration head."),
                SchemaState.Ahead => HealthCheckResult.Healthy("The schema is ahead of the compiled migration head."),
                SchemaState.NotApplicable => HealthCheckResult.Healthy("No PostgreSQL schema."),
                _ => HealthCheckResult.Healthy("The schema is at the compiled migration head."),
            };
        }
        catch (Exception ex) when (ex is Autonogrammer.Resilience.DataPlaneTimeoutException or Npgsql.NpgsqlException)
        {
            return HealthCheckResult.Unhealthy("The schema version could not be read.");
        }
    }
}
