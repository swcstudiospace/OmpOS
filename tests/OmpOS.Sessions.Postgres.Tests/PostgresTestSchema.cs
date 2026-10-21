using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace OmpOS.Sessions.Postgres.Tests;

/// <summary>
/// A fresh PostgreSQL schema for one test. Every data source it creates uses the schema as its search path, so the
/// production SQL (which names tables without a schema) runs unchanged and each test sees an empty store. Disposing
/// closes the data sources and drops the schema.
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

    /// <summary>Gets a connection string whose search path is this schema.</summary>
    internal string ConnectionString => new NpgsqlConnectionStringBuilder(_adminConnectionString) { SearchPath = SchemaName }.ConnectionString;

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

    /// <summary>Creates a data source that uses this schema. The schema owns it and disposes it.</summary>
    /// <returns>A new data source over this schema.</returns>
    internal NpgsqlDataSource CreateDataSource()
    {
        var dataSource = NpgsqlDataSource.Create(ConnectionString);
        _dataSources.Add(dataSource);
        return dataSource;
    }

    /// <summary>Disposes every data source this schema created, then drops the schema.</summary>
    /// <returns>A task that completes when the schema is gone.</returns>
    public async ValueTask DisposeAsync()
    {
        foreach (var dataSource in _dataSources)
        {
            await dataSource.DisposeAsync().ConfigureAwait(false);
        }

        _dataSources.Clear();
        await ExecuteAdminAsync($"DROP SCHEMA IF EXISTS \"{SchemaName}\" CASCADE", CancellationToken.None).ConfigureAwait(false);
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
