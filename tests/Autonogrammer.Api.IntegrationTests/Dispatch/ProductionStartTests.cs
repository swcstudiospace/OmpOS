using Autonogrammer.Api.Dispatch;
using Autonogrammer.Api.Options;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Coordination;
using Autonogrammer.Coordination.Dragonfly;
using Autonogrammer.Resilience;
using Autonogrammer.Sessions.Postgres;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>
/// A production host with dispatch off starts without a device store. Before the store was resolved lazily, the sweeper's constructor demanded
/// <c>IDispatchStore</c> at host start, and no store is registered outside Development without Postgres, so the host could not start.
/// </summary>
public sealed class ProductionStartTests
{
    /// <summary>The host starts in Production with Dispatch:Enabled=false and stops cleanly.</summary>
    [Fact]
    public async Task A_production_host_with_dispatch_off_starts_without_a_device_store()
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Dispatch:Enabled"] = "false",
            ["Sessions:Store"] = "InMemory",
        });
        builder.Services.AddOptions<ApiAuthOptions>();
        builder.Services.AddInMemoryCoordination();
        builder.Services.AddDispatchServices(builder.Environment);
        builder.Services.AddDispatchHub();

        using var host = builder.Build();
        await host.StartAsync(ct).ConfigureAwait(true);
        await host.StopAsync(ct).ConfigureAwait(true);
    }

    /// <summary>A Dispatch store observes the same open Postgres circuit as the rest of the host.</summary>
    [Fact]
    public async Task DispatchReadsRespectTheConfiguredPostgresCircuit()
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Sessions:Store"] = "Postgres",
            ["Sessions:Postgres:ConnectionString"] = "Host=127.0.0.1;Port=1;Database=test;Username=test;Password=test",
            ["DataPlane:Postgres:Resilience:MaxRetries"] = "0",
        });
        builder.Services.AddPostgresSessionStore(builder.Configuration);
        builder.Services.AddSingleton<ITenantHasher>(new TenantHasher(new TenantHasherOptions { Pepper = "test-pepper" }));
        builder.Services.AddRouting();
        builder.Services.AddDispatchServices(builder.Environment);
        using var host = builder.Build();
        var pipeline = host.Services.GetRequiredService<PostgresResilience>().Pipeline;
        for (var attempt = 0; attempt < ResilienceOptions.BreakerConsecutiveFailures && pipeline.State != DependencyState.Open; attempt++)
        {
            _ = await Should.ThrowAsync<IOException>(() => pipeline.ExecuteAsync<int>(OperationClass.Interactive, true, static _ => throw new IOException("down"), ct)).ConfigureAwait(true);
        }

        pipeline.State.ShouldBe(DependencyState.Open);
        var store = host.Services.GetRequiredService<IDispatchStore>();
        var failure = await Should.ThrowAsync<DataPlaneTimeoutException>(() => store.ListDevicesAsync("acct", ct).AsTask()).ConfigureAwait(true);
        failure.Reason.ShouldBe(DataPlaneTimeoutReason.CircuitOpen);
    }
}
