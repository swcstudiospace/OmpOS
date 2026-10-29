using Autonogrammer.Api.Options;
using Autonogrammer.Coordination;
using Autonogrammer.Coordination.Dragonfly;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Autonogrammer.Api.Coordination;

/// <summary>
/// Registers the coordination backend (run leases, idempotency records, session signals), its readiness check, and its startup step.
/// </summary>
/// <remarks>
/// The choice is made at composition time, unlike <c>Autonogrammer.Api.Persistence.SessionStoreRegistration</c>. The Dragonfly backend
/// takes its connection string when it is registered, so the store must be known before the host is built. The test host
/// (<c>ApiFactory</c>) applies its overrides after composition; a test that needs the Dragonfly backend must supply it
/// through the environment or <c>appsettings</c> files that exist at composition. Startup validation of
/// <see cref="CoordinationOptions"/> runs at host start for every value, so an unsupported or incomplete store fails startup
/// and names its key. The in-memory registration is the fallback branch only for that failing case; it never serves requests.
/// </remarks>
internal static class CoordinationRegistration
{
    /// <summary>Registers the coordination backend, its readiness check, and its startup step.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration, read once at composition for the store and the connection string.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApiCoordination(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var store = configuration[$"{CoordinationOptions.SectionName}:Store"];
        var connectionString = configuration[CoordinationOptions.DragonflyConnectionStringKey];
        if (CoordinationOptions.IsDragonflyStore(store) && !string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDragonflyCoordination(connectionString);
            services.AddHealthChecks().AddCheck<DragonflyCoordinationHealthCheck>("coordination", tags: ["ready"]);
        }
        else
        {
            services.AddInMemoryCoordination();
            services.AddHealthChecks().AddCheck("coordination", static _ => HealthCheckResult.Healthy("in-memory"), tags: ["ready"]);
        }

        services.AddHostedService<CoordinationStartup>();
        return services;
    }
}
