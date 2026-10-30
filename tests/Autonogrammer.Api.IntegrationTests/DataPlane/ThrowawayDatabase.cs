using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>
/// A private PostgreSQL database on the shared test server, so a test may revert migrations without touching the database that Production-shaped hosts share.
/// It is created from the <c>template_timescaledb</c> template when the server has it. Disposing drops it.
/// </summary>
internal sealed class ThrowawayDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;

    private ThrowawayDatabase(string adminConnectionString, string name)
    {
        _adminConnectionString = adminConnectionString;
        Name = name;
    }

    /// <summary>Gets the database name: <c>t_</c> and a GUID's hex digits.</summary>
    internal string Name { get; }

    /// <summary>Gets a connection string for the database with the administrative credentials of the server.</summary>
    internal string ConnectionString => new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = Name }.ConnectionString;

    /// <summary>Creates the database.</summary>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>The database.</returns>
    /// <exception cref="InvalidOperationException">The test run did not set the server connection string.</exception>
    internal static async Task<ThrowawayDatabase> CreateAsync(CancellationToken cancellationToken)
    {
        var admin = Environment.GetEnvironmentVariable(JwtTestHost.PostgresConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(admin))
        {
            throw new InvalidOperationException($"{JwtTestHost.PostgresConnectionEnvironmentVariable} is not set; run the test inside scripts/test-services.sh up.");
        }

        var database = new ThrowawayDatabase(admin, "t_" + Guid.NewGuid().ToString("N"));
        var template = await database.HasTemplateAsync(cancellationToken).ConfigureAwait(false) ? " TEMPLATE template_timescaledb" : string.Empty;
        await database.ExecuteAsync($"CREATE DATABASE \"{database.Name}\"{template}", cancellationToken).ConfigureAwait(false);
        return database;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() =>
        await ExecuteAsync($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)", CancellationToken.None).ConfigureAwait(false);

    private async Task<bool> HasTemplateAsync(CancellationToken cancellationToken)
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
        Justification = "The only dynamic part of the SQL is Name, which this class generates as t_ plus a GUID's hex digits and always quotes. No caller input reaches the text.")]
    private async Task ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
