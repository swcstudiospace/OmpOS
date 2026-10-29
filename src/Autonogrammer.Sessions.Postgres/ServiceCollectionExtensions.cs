using System.Globalization;
using Autonogrammer.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>Registers the PostgreSQL session store with the dependency-injection container.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>The configuration key that holds the PostgreSQL connection string. The environment form is <c>Sessions__Postgres__ConnectionString</c>.</summary>
    public const string ConnectionStringKey = "Sessions:Postgres:ConnectionString";

    /// <summary>The configuration key that holds the connection string of the migration role. Only the one-shot migrate service sets it. The environment form is <c>Sessions__Postgres__MigratorConnectionString</c>.</summary>
    public const string MigratorConnectionStringKey = "Sessions:Postgres:MigratorConnectionString";

    /// <summary>
    /// Registers <see cref="NpgsqlDataSource"/> (the runtime role), <see cref="PostgresResilience"/> and <see cref="PostgresSessionRepository"/> as singletons.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registration never throws, and it does not register <see cref="ISessionRepository"/>. The composition root decides
    /// which store is the session repository, so the choice can be made at resolution time.
    /// </para>
    /// <para>
    /// The connection string is read when <see cref="NpgsqlDataSource"/> is first resolved. A missing or blank value
    /// throws <see cref="InvalidOperationException"/> whose message names <see cref="ConnectionStringKey"/>. The data source
    /// uses the search path <c>autonogrammer,public</c>, whatever search path the string carries. The value is never logged.
    /// The pipeline every store command runs through reads the keys of <see cref="PostgresResilienceOptions.SectionName"/>; an absent key keeps its default
    /// and a value that is not a whole number fails naming the key.
    /// </para>
    /// <para>
    /// The store never creates schema objects: the <see cref="Migrations.MigrationRunner"/> does, as the migration role.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The configuration that supplies <see cref="ConnectionStringKey"/>.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddPostgresSessionStore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(_ => new PostgresSessionConnection(ReadConnectionString(configuration)));
        services.AddSingleton(provider => PostgresResilience.Create(
            ReadResilienceOptions(configuration),
            provider.GetService<TimeProvider>(),
            provider.GetService<ILogger<ResiliencePipeline>>()));
        services.AddSingleton(provider => provider.GetRequiredService<PostgresSessionConnection>().CreateDataSource(PostgresRole.Runtime, ReadResilienceOptions(configuration)));
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

    private static PostgresResilienceOptions ReadResilienceOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection(PostgresResilienceOptions.SectionName);
        var defaults = new PostgresResilienceOptions();
        return new PostgresResilienceOptions
        {
            ConnectSeconds = ReadInt(section, nameof(PostgresResilienceOptions.ConnectSeconds), defaults.ConnectSeconds),
            InteractiveCommandSeconds = ReadInt(section, nameof(PostgresResilienceOptions.InteractiveCommandSeconds), defaults.InteractiveCommandSeconds),
            BackgroundCommandSeconds = ReadInt(section, nameof(PostgresResilienceOptions.BackgroundCommandSeconds), defaults.BackgroundCommandSeconds),
            MaxRetries = ReadInt(section, nameof(PostgresResilienceOptions.MaxRetries), defaults.MaxRetries),
            MinDelayMs = ReadInt(section, nameof(PostgresResilienceOptions.MinDelayMs), defaults.MinDelayMs),
            MaxDelayMs = ReadInt(section, nameof(PostgresResilienceOptions.MaxDelayMs), defaults.MaxDelayMs),
            BudgetSeconds = ReadInt(section, nameof(PostgresResilienceOptions.BudgetSeconds), defaults.BudgetSeconds),
            PoolInteractive = ReadInt(section, nameof(PostgresResilienceOptions.PoolInteractive), defaults.PoolInteractive),
            PoolBackground = ReadInt(section, nameof(PostgresResilienceOptions.PoolBackground), defaults.PoolBackground),
            BreakerOpenSeconds = ReadInt(section, nameof(PostgresResilienceOptions.BreakerOpenSeconds), defaults.BreakerOpenSeconds),
        };
    }

    private static int ReadInt(IConfigurationSection section, string key, int fallback)
    {
        var text = section[key];
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException($"Configuration key '{section.Path}:{key}' must be a whole number.");
        }

        return value;
    }
}
