using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Autonogrammer.Api.Hosting;

/// <summary>Readiness check that turns unhealthy once the host starts stopping, so the platform stops routing new requests here.</summary>
internal sealed class HostingPendingShutdownCheck : IHealthCheck
{
    private readonly IHostApplicationLifetime _lifetime;

    /// <summary>Initializes a new instance of the <see cref="HostingPendingShutdownCheck"/> class.</summary>
    /// <param name="lifetime">The application lifetime.</param>
    public HostingPendingShutdownCheck(IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(_lifetime.ApplicationStopping.IsCancellationRequested
            ? HealthCheckResult.Unhealthy("The host is shutting down.")
            : HealthCheckResult.Healthy("The host is not shutting down."));
}
