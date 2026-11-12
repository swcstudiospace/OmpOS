using Npgsql;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>
/// Runs the shared <see cref="ISessionRepository"/> contract against <see cref="PostgresSessionRepository"/>. Each test
/// gets its own schema on the shared PostgreSQL server, migrated with the production <see cref="Migrations.MigrationRunner"/>.
/// </summary>
[Collection(PostgresServerGroup.Name)]
public sealed class PostgresSessionRepositoryContractTests(PostgresServerFixture server)
    : Autonogrammer.Sessions.Tests.SessionRepositoryContractTests<PostgresSessionRepository>
{
    private readonly Dictionary<PostgresSessionRepository, PostgresTestSchema> _schemas = new();

    /// <inheritdoc />
    protected override async Task<PostgresSessionRepository> CreateRepositoryAsync(CancellationToken cancellationToken)
    {
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, cancellationToken).ConfigureAwait(false);
        var dataSource = schema.CreateDataSource();
        await ProvisionAsync(dataSource, cancellationToken, "dev", "alice", "bob").ConfigureAwait(false);

        var repository = new PostgresSessionRepository(dataSource, schema.Connection);
        _schemas.Add(repository, schema);
        return repository;
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeRepositoryAsync(PostgresSessionRepository repository)
    {
        if (_schemas.Remove(repository, out var schema))
        {
            await schema.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Provisions accounts as the provisioning step does: one lifecycle row each, committed before the account's first write.</summary>
    private static async Task ProvisionAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken, params string[] accountIds)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            foreach (var accountId in accountIds)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO account_lifecycle (account_id) VALUES (@account_id) ON CONFLICT (account_id) DO NOTHING";
                command.Parameters.AddWithValue("account_id", accountId);
                _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
