using System.Net;
using Autonogrammer.Api.Hosting;
using Autonogrammer.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Hosting;

/// <summary>Pins the startup probe, the startup gate service and the readiness flip on shutdown.</summary>
public sealed class HealthProbeTests
{
    private static void MapReady(WebApplication app) =>
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = static check => check.Tags.Contains(HostingPolicies.ReadyTag),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = 200,
                [HealthStatus.Degraded] = 503,
                [HealthStatus.Unhealthy] = 503,
            },
        });

    private static async Task<HttpStatusCode> StatusAsync(WebApplication app, string path, CancellationToken testToken)
    {
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), testToken).ConfigureAwait(false);
        return response.StatusCode;
    }

    /// <summary>The startup probe is 503 while the gate is closed and 200 once it opens.</summary>
    [Fact]
    public async Task Startup_IsUnhealthyUntilTheGateOpens()
    {
        var ct = TestContext.Current.CancellationToken;
        var app = await HostingTestHost.StartAsync(
            ct,
            configureServices: static services => services.AddSingleton<ISessionRepository>(static _ => new FailingSessionRepository())).ConfigureAwait(true);
        await using var appScope = app.ConfigureAwait(true);
        var gate = app.Services.GetRequiredService<HostingStartupGate>();

        gate.IsOpen.ShouldBeFalse();
        (await StatusAsync(app, "/health/startup", ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.ServiceUnavailable);

        gate.Open();

        (await StatusAsync(app, "/health/startup", ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>The gate service opens the gate once the store answers, and the probe turns healthy.</summary>
    [Fact]
    public async Task GateService_OpensTheGateWhenTheStoreIsReachable()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new HostingStartupGate();
        using var service = new StartupGateHostedService(
            new InMemorySessionRepository(),
            gate,
            TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<StartupGateHostedService>.Instance);

        await service.StartAsync(ct).ConfigureAwait(true);
        await service.ExecuteTask.ShouldNotBeNull().ConfigureAwait(true);

        gate.IsOpen.ShouldBeTrue();
        await service.StopAsync(ct).ConfigureAwait(true);
    }

    /// <summary>Readiness is 200 while running and 503 once the host starts stopping.</summary>
    [Fact]
    public async Task Ready_FlipsToUnhealthyWhenTheHostIsStopping()
    {
        var ct = TestContext.Current.CancellationToken;
        var app = await HostingTestHost.StartAsync(ct, mapEndpoints: MapReady).ConfigureAwait(true);
        await using var appScope = app.ConfigureAwait(true);

        (await StatusAsync(app, "/health/ready", ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.OK);

        app.Lifetime.StopApplication();

        (await StatusAsync(app, "/health/ready", ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.ServiceUnavailable);
    }
}
