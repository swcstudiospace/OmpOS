using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>
/// A fresh PostgreSQL database for one test, created on the shared server. The registered data source sets its own
/// search path (<c>autonogrammer,public</c>), so a test that goes through it cannot be isolated by a schema: its tables land in
/// the shared <c>autonogrammer</c> schema. The database is created from the <c>template_timescaledb</c> template the TimescaleDB image ships when
/// the server has it, so the extension (which no migration step may create) is already in it; otherwise from the default template, which the
/// image also prepares. Disposing drops the database and everything in it.
/// </summary>
internal sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;

    private PostgresTestDatabase(string adminConnectionString, string databaseName)
    {
        _adminConnectionString = adminConnectionString;
        DatabaseName = databaseName;
    }

    /// <summary>Gets the database name.</summary>
    internal string DatabaseName { get; }

    /// <summary>Gets a connection string for this database, with the administrative credentials of the server.</summary>
    internal string ConnectionString =>
        new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = DatabaseName }.ConnectionString;

    /// <summary>Creates an empty database on the server.</summary>
    /// <param name="adminConnectionString">An administrative connection string for the server.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new database.</returns>
    internal static async Task<PostgresTestDatabase> CreateAsync(string adminConnectionString, CancellationToken cancellationToken)
    {
        var database = new PostgresTestDatabase(adminConnectionString, "t_" + Guid.NewGuid().ToString("N"));
        if (await database.HasTimescaleTemplateAsync(cancellationToken).ConfigureAwait(false))
        {
            await database.ExecuteAdminAsync($"CREATE DATABASE \"{database.DatabaseName}\" TEMPLATE template_timescaledb", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await database.ExecuteAdminAsync($"CREATE DATABASE \"{database.DatabaseName}\"", cancellationToken).ConfigureAwait(false);
        }

        return database;
    }

    /// <summary>Wraps a database that something else creates (the bootstrap script), so that disposing drops it. Dropping a database that was never created does nothing.</summary>
    /// <param name="adminConnectionString">An administrative connection string for the server.</param>
    /// <param name="databaseName">The name of the database, a <c>t_</c> prefix and a GUID's hex digits.</param>
    /// <returns>The wrapper that owns the cleanup.</returns>
    internal static PostgresTestDatabase ForExisting(string adminConnectionString, string databaseName) => new(adminConnectionString, databaseName);

    /// <summary>Drops the database, including any connection that is still open to it.</summary>
    /// <returns>A task that completes when the database is gone.</returns>
    public async ValueTask DisposeAsync() =>
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)", CancellationToken.None).ConfigureAwait(false);

    /// <summary>Says whether the server has the <c>template_timescaledb</c> template database the TimescaleDB image ships.</summary>
    private async Task<bool> HasTimescaleTemplateAsync(CancellationToken cancellationToken)
    {
        using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'template_timescaledb' AND datistemplate)";
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The only dynamic part of the SQL is DatabaseName, which this class generates as \"t_\" plus a GUID's hex digits and always quotes. No caller input reaches the text, and identifiers cannot be bound as command parameters.")]
    private async Task ExecuteAdminAsync(string sql, CancellationToken cancellationToken)
    {
        using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
