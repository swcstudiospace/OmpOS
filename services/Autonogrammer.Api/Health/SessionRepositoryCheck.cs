using Autonogrammer.Sessions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Autonogrammer.Api.Health;

/// <summary>
/// Session-store probe for <c>/health/ready</c>: reads a guaranteed-absent id, which exercises
/// the store without writing.
/// </summary>
internal sealed class SessionRepositoryCheck : IHealthCheck
{
    private readonly ISessionRepository _repository;

    /// <summary>Initializes a new instance of the <see cref="SessionRepositoryCheck"/> class.</summary>
    /// <param name="repository">The session store.</param>
    public SessionRepositoryCheck(ISessionRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var probe = await _repository.GetAsync("__health__", cancellationToken).ConfigureAwait(false);
            return probe is null
                ? HealthCheckResult.Healthy("Session store is reachable.")
                : HealthCheckResult.Degraded("Session store returned an unexpected health probe session.");
        }
#pragma warning disable CA1031 // A readiness probe must report, never throw; any store failure is the signal.
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Session store is unreachable.", ex);
        }
#pragma warning restore CA1031
    }
}
