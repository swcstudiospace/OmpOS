using Autonogrammer.Api.Dispatch;
using Autonogrammer.Api.Options;
using Autonogrammer.Coordination;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
}
