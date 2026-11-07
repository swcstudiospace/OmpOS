using Autonogrammer.Resilience;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>
/// Readiness probe that sends PING to Dragonfly through the resilience pipeline. Any outage reports unhealthy, and the result carries the
/// <see cref="DependencyState"/> of the breaker for the data-plane status service.
/// </summary>
public sealed class DragonflyCoordinationHealthCheck : IHealthCheck
{
    /// <summary>The key of the breaker state in <see cref="HealthCheckResult.Data"/>.</summary>
    public const string DependencyStateKey = "dependencyState";

    private readonly IDatabase _database;
    private readonly DragonflyResilience _resilience;

    /// <summary>Creates a probe over the database of <paramref name="multiplexer"/>.</summary>
    /// <param name="multiplexer">The connection to Dragonfly. The caller owns its lifetime.</param>
    /// <param name="resilience">The pipeline of the connection. Null builds one from the defaults.</param>
    public DragonflyCoordinationHealthCheck(IConnectionMultiplexer multiplexer, DragonflyResilience? resilience = null)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        _database = multiplexer.GetDatabase();
        _resilience = resilience ?? DragonflyResilience.Create();
    }

    /// <summary>Gets what the breaker says about Dragonfly right now.</summary>
    public DependencyState State => _resilience.State;

    /// <summary>Sends PING and reports the result.</summary>
    /// <param name="context">The health check context supplied by the host.</param>
    /// <param name="cancellationToken">Stops waiting for the reply when cancelled.</param>
    /// <returns>Healthy when Dragonfly answers; unhealthy otherwise. The data carries the breaker state.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _resilience.ClosedAsync(true, () => _database.PingAsync(), cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("Dragonfly answered PING.", Data());
        }
        catch (DataPlaneTimeoutException)
        {
            return HealthCheckResult.Unhealthy("Dragonfly PING failed.", data: Data());
        }
        catch (RedisException exception)
        {
            return HealthCheckResult.Unhealthy("Dragonfly PING failed.", exception, Data());
        }
    }

    private Dictionary<string, object> Data()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal) { [DependencyStateKey] = _resilience.State.ToString() };
    }
}
