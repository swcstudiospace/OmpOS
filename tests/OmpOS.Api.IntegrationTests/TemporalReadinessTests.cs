using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OmpOS.Api.Health;
using OmpOS.Api.Options;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Verifies the Temporal readiness check probes the configured endpoint over TCP.
/// </summary>
public sealed class TemporalReadinessTests
{
    /// <summary>Verifies an endpoint that accepts connections reports healthy.</summary>
    [Fact]
    public async Task Enabled_WithReachableEndpoint_ReturnsHealthy()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var check = new TemporalCheck(CreateOptions(true, $"http://127.0.0.1:{port}"));

            var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

            result.Status.ShouldBe(HealthStatus.Healthy);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Verifies an endpoint with a closed port reports unhealthy.</summary>
    [Fact]
    public async Task Enabled_WithClosedPort_ReturnsUnhealthy()
    {
        var port = GetFreePort();
        var check = new TemporalCheck(CreateOptions(true, $"http://127.0.0.1:{port}"));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    /// <summary>Verifies an endpoint string that cannot be parsed reports unhealthy.</summary>
    [Fact]
    public async Task Enabled_WithUnparsableEndpoint_ReturnsUnhealthy()
    {
        var check = new TemporalCheck(CreateOptions(true, "http://"));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    /// <summary>Verifies disabled workflows report healthy without attempting a connection.</summary>
    [Fact]
    public async Task Disabled_ReturnsHealthyWithoutConnecting()
    {
        var port = GetFreePort();
        var check = new TemporalCheck(CreateOptions(false, $"http://127.0.0.1:{port}"));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>Verifies enabled workflows without an endpoint report degraded.</summary>
    [Fact]
    public async Task Enabled_WithoutEndpoint_ReturnsDegraded()
    {
        var check = new TemporalCheck(CreateOptions(true, null));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>Creates workflows options with the given enablement and endpoint.</summary>
    /// <param name="enabled">Whether durable workflows are enabled.</param>
    /// <param name="endpoint">The configured Temporal endpoint, if any.</param>
    /// <returns>Options wrapping the configured values.</returns>
    private static Microsoft.Extensions.Options.IOptions<WorkflowsOptions> CreateOptions(bool enabled, string? endpoint)
    {
        return Microsoft.Extensions.Options.Options.Create(new WorkflowsOptions
        {
            Enabled = enabled,
            TemporalEndpoint = endpoint,
        });
    }

    /// <summary>Binds a listener to reserve a free port, then releases it.</summary>
    /// <returns>A port that is closed at the time of return.</returns>
    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
