using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Sessions.Postgres.Migrations;
using Npgsql;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>
/// A fresh PostgreSQL schema for one test. Every data source it creates uses the schema, then public, as its search path, so
/// the production SQL (which names tables without a schema, and calls the TimescaleDB functions that live in public) runs
/// unchanged and each test sees an empty store. Disposing closes the data sources and drops the schema.
/// </summary>
internal sealed class PostgresTestSchema : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly List<NpgsqlDataSource> _dataSources = [];

    private PostgresTestSchema(string adminConnectionString, string schemaName)
    {
        _adminConnectionString = adminConnectionString;
        SchemaName = schemaName;
    }

    /// <summary>Gets the schema name.</summary>
    internal string SchemaName { get; }

    /// <summary>Gets a connection string whose search path is this schema, then public.</summary>
    internal string ConnectionString => new NpgsqlConnectionStringBuilder(_adminConnectionString) { SearchPath = $"{SchemaName},public" }.ConnectionString;

    /// <summary>Creates an empty schema on the server.</summary>
    /// <param name="adminConnectionString">An administrative connection string for the server.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new schema.</returns>
    internal static async Task<PostgresTestSchema> CreateAsync(string adminConnectionString, CancellationToken cancellationToken)
    {
        var schema = new PostgresTestSchema(adminConnectionString, "t_" + Guid.NewGuid().ToString("N"));
        await schema.ExecuteAdminAsync($"CREATE SCHEMA \"{schema.SchemaName}\"", cancellationToken).ConfigureAwait(false);
        return schema;
    }

    /// <summary>Creates a schema on the server and migrates it to the head with the production <see cref="MigrationRunner"/>, so the tables exist as they do in the API.</summary>
    /// <param name="adminConnectionString">An administrative connection string for the server.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new, migrated schema.</returns>
    internal static async Task<PostgresTestSchema> CreateMigratedAsync(string adminConnectionString, CancellationToken cancellationToken)
    {
        var schema = await CreateAsync(adminConnectionString, cancellationToken).ConfigureAwait(false);
        await schema.MigrateAsync(cancellationToken).ConfigureAwait(false);
        return schema;
    }

    /// <summary>Runs the migration runner into this schema, as the migration role would: <c>schema_migrations</c> and every table land in this schema.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The versions applied by this call.</returns>
    internal Task<IReadOnlyList<int>> MigrateAsync(CancellationToken cancellationToken) =>
        new MigrationRunner(CreateDataSource(), SchemaName).MigrateAsync(cancellationToken);

    /// <summary>Creates a data source that uses this schema. The schema owns it and disposes it.</summary>
    /// <returns>A new data source over this schema.</returns>
    internal NpgsqlDataSource CreateDataSource()
    {
        var dataSource = NpgsqlDataSource.Create(ConnectionString);
        _dataSources.Add(dataSource);
        return dataSource;
    }

    /// <summary>Gets the connection the store is configured with for this schema. Session locks connect with it directly.</summary>
    internal PostgresSessionConnection Connection => new(ConnectionString);

    /// <summary>Creates a repository over a new data source that uses this schema, configured as the store is.</summary>
    /// <returns>A repository whose data source the schema owns.</returns>
    internal PostgresSessionRepository CreateRepository() => new(CreateDataSource(), Connection);

    /// <summary>Disposes every data source this schema created, then drops the schema.</summary>
    /// <returns>A task that completes when the schema is gone.</returns>
    public async ValueTask DisposeAsync()
    {
        foreach (var dataSource in _dataSources)
        {
            await dataSource.DisposeAsync().ConfigureAwait(false);
        }

        _dataSources.Clear();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await RemoveBackgroundJobsAsync(CancellationToken.None).ConfigureAwait(false);
                await ExecuteAdminAsync($"DROP SCHEMA IF EXISTS \"{SchemaName}\" CASCADE", CancellationToken.None).ConfigureAwait(false);
                return;
            }
            catch (PostgresException failure) when (string.Equals(failure.SqlState, "40P01", StringComparison.Ordinal) && attempt < 5)
            {
                // The retention policies the migrations add start TimescaleDB background jobs, which can take catalog locks against the drop. The server chose
                // this statement as the deadlock victim and nothing was dropped, so run it again.
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Removes the TimescaleDB background jobs (the retention policies) of this schema's hypertables, so that none of them runs against the drop.</summary>
    private async Task RemoveBackgroundJobsAsync(CancellationToken cancellationToken)
    {
        using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT delete_job(job_id) FROM timescaledb_information.jobs WHERE hypertable_schema = @schema";
        command.Parameters.AddWithValue("schema", SchemaName);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The only dynamic part of the SQL is SchemaName, which this class generates as \"t_\" plus a GUID's hex digits and always quotes. No caller input reaches the text, and identifiers cannot be bound as command parameters.")]
    private async Task ExecuteAdminAsync(string sql, CancellationToken cancellationToken)
    {
        using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
