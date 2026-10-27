using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OmpOS.AgentWorker.Bridge;
using OmpOS.AgentWorker.Configuration;
using OmpOS.AgentWorker.Health;
using OmpOS.AgentWorker.Security;

namespace OmpOS.AgentWorker;

/// <summary>
/// Builds the agent worker host. The worker registers no telemetry exporter, no HTTP client and no database connection: Railway
/// Serverless puts a service to sleep only after five minutes without outbound traffic, so everything the worker does must be inbound.
/// Every option is validated on start, so a missing public key or a bad path stops the process instead of failing each connection.
/// </summary>
internal static class WorkerApp
{
    /// <summary>Builds the host without starting it.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="configure">Optional hook that runs after the worker's own registrations, for tests that replace the clock, add configuration or bind a loopback port.</param>
    /// <returns>The built host.</returns>
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(static options => options.AddServerHeader = false);

        // Request logging would write a line per health probe; authentication failures stay visible for operators.
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.Information);

        builder.Services.AddOptions<WorkerOptions>().BindConfiguration(WorkerOptions.SectionName).ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<WorkerOptions>, WorkerOptionsValidator>();
        builder.Services.AddOptions<WorkerAuthOptions>().BindConfiguration(WorkerAuthOptions.SectionName).ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<WorkerAuthOptions>, WorkerAuthOptionsValidator>();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<WorkerJwtValidator>();
        builder.Services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, WorkerJwtBearerSetup>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        builder.Services.AddSingleton<WorkerSecretRedactor>();
        builder.Services.AddSingleton<OmpProcessHost>();
        builder.Services.AddSingleton<WorkerReadiness>();
        builder.Services.AddOptions<WebSocketOptions>()
            .Configure<IOptions<WorkerOptions>>(static (socketOptions, worker) => socketOptions.KeepAliveInterval = worker.Value.PingInterval);

        configure?.Invoke(builder);

        var app = builder.Build();
        app.UseWebSockets();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapWorkerHealth();
        app.MapAcpBridge();
        return app;
    }
}
