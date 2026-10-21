using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OmpOS.Api.Health;

/// <summary>
/// Harness adapter readiness for <c>/health/ready</c>. The Phase 2 in-memory adapter needs
/// no external process, so it is always ready; later adapters report real connectivity here.
/// </summary>
internal sealed class HarnessReadinessCheck : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(HealthCheckResult.Healthy("In-memory harness adapter is ready."));
    }
}
