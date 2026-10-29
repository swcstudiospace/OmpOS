using Autonogrammer.Api.Hosting;
using Autonogrammer.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Api.IntegrationTests.Hosting;

/// <summary>Builds a minimal in-memory host that wires only the hosting extensions, so each test pins one behaviour.</summary>
internal static class HostingTestHost
{
    /// <summary>Starts a test host with the hosting extensions in the contract order.</summary>
    /// <param name="testToken">The test cancellation token.</param>
    /// <param name="environment">The host environment name.</param>
    /// <param name="config">Configuration overrides.</param>
    /// <param name="configureServices">Extra service registrations, applied before the hosting extensions.</param>
    /// <param name="mapEndpoints">Registers the stub endpoints.</param>
    /// <returns>The running application (caller disposes).</returns>
    public static async Task<WebApplication> StartAsync(
        CancellationToken testToken,
        string environment = "Production",
        Dictionary<string, string?>? config = null,
        Action<IServiceCollection>? configureServices = null,
        Action<WebApplication>? mapEndpoints = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(config ?? []);
        builder.Services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
        configureServices?.Invoke(builder.Services);
        _ = builder.AddHostingHardening();
        builder.Logging.ClearProviders();

        var app = builder.Build();
        _ = app.UseHostingHardening();
        mapEndpoints?.Invoke(app);
        _ = app.MapHostingEndpoints();
        await app.StartAsync(testToken).ConfigureAwait(false);
        return app;
    }
}
