using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace OmpOS.Coordination.Dragonfly;

/// <summary>Readiness probe that sends PING to Dragonfly. Any Redis failure reports unhealthy.</summary>
public sealed class DragonflyCoordinationHealthCheck : IHealthCheck
{
    private readonly IDatabase _database;

    /// <summary>Creates a probe over the database of <paramref name="multiplexer"/>.</summary>
    /// <param name="multiplexer">The connection to Dragonfly. The caller owns its lifetime.</param>
    public DragonflyCoordinationHealthCheck(IConnectionMultiplexer multiplexer)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        _database = multiplexer.GetDatabase();
    }

    /// <summary>Sends PING and reports the result.</summary>
    /// <param name="context">The health check context supplied by the host.</param>
    /// <param name="cancellationToken">Stops waiting for the reply when cancelled.</param>
    /// <returns>Healthy when Dragonfly answers; unhealthy with the Redis exception otherwise.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.PingAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("Dragonfly answered PING.");
        }
        catch (RedisException exception)
        {
            return HealthCheckResult.Unhealthy("Dragonfly PING failed.", exception);
        }
    }
}
