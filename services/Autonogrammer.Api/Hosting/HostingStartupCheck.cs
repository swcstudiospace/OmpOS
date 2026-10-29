using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Autonogrammer.Api.Hosting;

/// <summary>Startup probe for <c>/health/startup</c>: unhealthy until <see cref="HostingStartupGate"/> opens.</summary>
internal sealed class HostingStartupCheck : IHealthCheck
{
    private readonly HostingStartupGate _gate;

    /// <summary>Initializes a new instance of the <see cref="HostingStartupCheck"/> class.</summary>
    /// <param name="gate">The startup gate.</param>
    public HostingStartupCheck(HostingStartupGate gate)
    {
        _gate = gate;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(_gate.IsOpen
            ? HealthCheckResult.Healthy("Startup completed.")
            : HealthCheckResult.Unhealthy("Startup has not completed."));
}
