using Microsoft.Extensions.Options;
using OmpOS.Api.Options;
using OmpOS.Sessions;
using OmpOS.Sessions.Postgres;

namespace OmpOS.Api.Persistence;

/// <summary>
/// Registers the API's session store. <see cref="ISessionRepository"/> is resolved per use: when <c>Workflows:Enabled</c> is
/// true it is the PostgreSQL session log that the API and the run worker share, so a routed run's events, approvals and
/// settlement are visible to both processes. When false it is the in-process <see cref="InMemorySessionRepository"/>, exactly
/// as before.
/// </summary>
/// <remarks>
/// The choice is made at resolution time, like the dispatcher: the test host applies configuration after the top-level
/// statements of <c>Program</c> have run, so a composition-time branch would not see its overrides. The PostgreSQL
/// registration does not read the connection string until its data source is first resolved, so a default configuration never
/// needs it. Startup validation of the enabled path lives in <see cref="SessionStoreStartup"/>.
/// </remarks>
internal static class SessionStoreRegistration
{
    /// <summary>Registers the session store and its startup schema step.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration (the source of the connection string).</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApiSessionStore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddPostgresSessionStore(configuration);
        services.AddSingleton<InMemorySessionRepository>();
        services.AddSingleton<ISessionRepository>(static provider =>
            provider.GetRequiredService<IOptions<WorkflowsOptions>>().Value.Enabled
                ? (ISessionRepository)provider.GetRequiredService<PostgresSessionRepository>()
                : provider.GetRequiredService<InMemorySessionRepository>());
        services.AddHostedService<SessionStoreStartup>();
        return services;
    }
}
