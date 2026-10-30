using Autonogrammer.Api.Options;
using Autonogrammer.Sessions;
using Autonogrammer.Sessions.Postgres;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Persistence;

/// <summary>
/// Registers the API's session store. <see cref="ISessionRepository"/> is chosen from <c>Sessions:Store</c>: the PostgreSQL
/// session log when it is <c>Postgres</c>, so every replica reads and writes one log; otherwise the in-process
/// <see cref="InMemorySessionRepository"/>.
/// </summary>
/// <remarks>
/// The choice is made at resolution time, not at composition time. The test host (<c>ApiFactory</c>) applies its
/// configuration overrides after the top-level statements of <c>Program</c> have run, so a composition-time branch would not
/// see them. Resolution-time selection reads the bound <see cref="SessionsOptions"/>, the configuration the host actually runs
/// with. The PostgreSQL registration reads its connection string only when its data source is first resolved, so the in-memory
/// store never needs it. Startup validation of the bound options runs at host start; <see cref="SessionStoreStartup"/> creates
/// the schema for the PostgreSQL store.
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
            SessionsOptions.IsPostgresStore(provider.GetRequiredService<IOptions<SessionsOptions>>().Value.Store)
                ? (ISessionRepository)provider.GetRequiredService<PostgresSessionRepository>()
                : provider.GetRequiredService<InMemorySessionRepository>());
        services.AddHostedService<SessionStoreStartup>();
        return services;
    }
}
