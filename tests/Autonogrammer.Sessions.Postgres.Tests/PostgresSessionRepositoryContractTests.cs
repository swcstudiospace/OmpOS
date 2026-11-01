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
}
