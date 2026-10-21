using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace OmpOS.Sessions.Postgres;

/// <summary>Registers the PostgreSQL session store with the dependency-injection container.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>The configuration key that holds the PostgreSQL connection string. The environment form is <c>Sessions__Postgres__ConnectionString</c>.</summary>
    public const string ConnectionStringKey = "Sessions:Postgres:ConnectionString";

    /// <summary>
    /// Registers <see cref="NpgsqlDataSource"/> and <see cref="PostgresSessionRepository"/> as singletons.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registration never throws, and it does not register <see cref="ISessionRepository"/>. The composition root decides
    /// which store is the session repository, so a configuration switch such as <c>Workflows:Enabled</c> can select the
    /// store at resolution time.
    /// </para>
    /// <para>
    /// The connection string is read when <see cref="NpgsqlDataSource"/> is first resolved. A missing or blank value
    /// throws <see cref="InvalidOperationException"/> whose message names <see cref="ConnectionStringKey"/>. The value is
    /// never logged.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The configuration that supplies <see cref="ConnectionStringKey"/>.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddPostgresSessionStore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(_ => NpgsqlDataSource.Create(ReadConnectionString(configuration)));
        services.AddSingleton<PostgresSessionRepository>();
        return services;
    }

    /// <summary>Reads the connection string, failing with the key name when it is missing or blank.</summary>
    /// <param name="configuration">The configuration to read.</param>
    /// <returns>The non-blank connection string.</returns>
    /// <exception cref="InvalidOperationException">The key is missing or blank.</exception>
    private static string ReadConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration[ConnectionStringKey];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Configuration key '{ConnectionStringKey}' (environment variable 'Sessions__Postgres__ConnectionString') " +
                "is required for the PostgreSQL session store.");
        }

        return connectionString;
    }
}
