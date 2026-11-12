using Autonogrammer.Sessions.Postgres.Migrations;
using Npgsql;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Stores.Tests.Support;

/// <summary>
/// Applies M0006 into a throwaway schema. The connection comes from <c>AUTONOGRAMMER_TEST_POSTGRES_CONNECTION</c>.
/// An unset variable throws; these tests never skip.
/// </summary>
internal sealed class PostgresArtifactFixture : IAsyncLifetime
{
    private readonly List<NpgsqlDataSource> _sources = [];
    private string _admin = string.Empty;

    /// <summary>Gets the schema the store's search path selects.</summary>
    public string SchemaName { get; } = "t_" + Guid.NewGuid().ToString("N");

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION is unset. Source scripts/test-services.sh up before these tests.");
        }

        _admin = configured;
        var admin = NpgsqlDataSource.Create(_admin);
        _sources.Add(admin);
        await new MigrationRunner(admin, SchemaName).MigrateAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Provisions the account, then creates a store whose data source search path is this schema.</summary>
    /// <param name="accountId">The account the store filters on.</param>
    /// <returns>The store.</returns>
    public async ValueTask<PostgresArtifactStore> CreateStoreAsync(string accountId)
    {
        await ProvisionAccountAsync(accountId).ConfigureAwait(false);
        var builder = new NpgsqlConnectionStringBuilder(_admin) { SearchPath = SchemaName + ",public" };
        var source = NpgsqlDataSource.Create(builder.ConnectionString);
        _sources.Add(source);
        return new PostgresArtifactStore(source, accountId);
    }

    /// <summary>Inserts the lifecycle row that write admission requires. Writes never insert it.</summary>
    /// <param name="accountId">The account to provision.</param>
    /// <returns>A task that completes when the row exists.</returns>
    private async Task ProvisionAccountAsync(string accountId)
    {
        var builder = new NpgsqlConnectionStringBuilder(_admin) { SearchPath = SchemaName };
        var connection = new NpgsqlConnection(builder.ConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            using var command = new NpgsqlCommand(
                "INSERT INTO account_lifecycle (account_id) VALUES (@id) ON CONFLICT (account_id) DO NOTHING",
                connection);
            command.Parameters.AddWithValue("id", accountId);
            _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var source in _sources)
        {
            await source.DisposeAsync().ConfigureAwait(false);
        }

        _sources.Clear();
        if (_admin.Length == 0)
        {
            return;
        }

        var connection = new NpgsqlConnection(_admin);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            var set = new NpgsqlCommand("SELECT set_config('autonogrammer.drop_schema', @schema, false)", connection);
            await using (set.ConfigureAwait(false))
            {
                set.Parameters.AddWithValue("schema", SchemaName);
                _ = await set.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            var drop = new NpgsqlCommand(
                """
                DO $drop$
                BEGIN
                    IF EXISTS (SELECT 1 FROM timescaledb_information.jobs WHERE hypertable_schema = current_setting('autonogrammer.drop_schema')) THEN
                        PERFORM delete_job(job_id) FROM timescaledb_information.jobs WHERE hypertable_schema = current_setting('autonogrammer.drop_schema');
                    END IF;
                    EXECUTE format('DROP SCHEMA IF EXISTS %I CASCADE', current_setting('autonogrammer.drop_schema'));
                END
                $drop$;
                """,
                connection);
            await using (drop.ConfigureAwait(false))
            {
                await drop.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
    }
}
