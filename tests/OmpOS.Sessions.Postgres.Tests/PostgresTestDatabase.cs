using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace OmpOS.Sessions.Postgres.Tests;

/// <summary>
/// A fresh PostgreSQL database for one test, created on the shared server. The registered data source sets its own
/// search path (<c>ompos,public</c>), so a test that goes through it cannot be isolated by a schema: its tables land in
/// the shared <c>ompos</c> schema. Disposing drops the database and everything in it.
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
        await database.ExecuteAdminAsync($"CREATE DATABASE \"{database.DatabaseName}\"", cancellationToken).ConfigureAwait(false);
        return database;
    }

    /// <summary>Drops the database, including any connection that is still open to it.</summary>
    /// <returns>A task that completes when the database is gone.</returns>
    public async ValueTask DisposeAsync() =>
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)", CancellationToken.None).ConfigureAwait(false);

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
