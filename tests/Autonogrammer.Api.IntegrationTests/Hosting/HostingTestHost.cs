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
    /// <summary>Starts a test host with the hosting extensions in the order of <c>Program.cs</c>: hardening, the exception handler when asked for, then request timeouts.</summary>
    /// <param name="testToken">The test cancellation token.</param>
    /// <param name="environment">The host environment name.</param>
    /// <param name="config">Configuration overrides.</param>
    /// <param name="configureServices">Extra service registrations, applied before the hosting extensions.</param>
    /// <param name="mapEndpoints">Registers the stub endpoints.</param>
    /// <param name="exceptionHandler">Whether to add the Production exception handler, as <c>Program.cs</c> does outside Development.</param>
    /// <returns>The running application (caller disposes).</returns>
    public static async Task<WebApplication> StartAsync(
        CancellationToken testToken,
        string environment = "Production",
        Dictionary<string, string?>? config = null,
        Action<IServiceCollection>? configureServices = null,
        Action<WebApplication>? mapEndpoints = null,
        bool exceptionHandler = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(config ?? []);
        builder.Services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
        _ = builder.Services.AddProblemDetails();
        configureServices?.Invoke(builder.Services);
        _ = builder.AddHostingHardening();
        builder.Logging.ClearProviders();

        var app = builder.Build();
        _ = app.UseHostingHardening();
        if (exceptionHandler)
        {
            _ = app.UseExceptionHandler();
        }

        _ = app.UseHostingRequestTimeouts();
        mapEndpoints?.Invoke(app);
        _ = app.MapHostingEndpoints();
        await app.StartAsync(testToken).ConfigureAwait(false);
        return app;
    }
}
