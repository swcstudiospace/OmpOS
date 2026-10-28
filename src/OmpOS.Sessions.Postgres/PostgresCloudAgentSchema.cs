using Npgsql;

namespace OmpOS.Sessions.Postgres;

/// <summary>
/// Creates the PostgreSQL schema behind <see cref="PostgresCloudAgentStore"/>: the <c>ompos</c> schema and the <c>cloud_agents</c>
/// table. Like <see cref="PostgresSessionSchema"/>, creation is idempotent: every statement is guarded by <c>IF NOT EXISTS</c> and
/// the batch runs under a transaction-scoped advisory lock, so concurrent starts serialize instead of racing on the catalog.
/// </summary>
/// <remarks>
/// <para>
/// <c>cloud_agents</c> holds one row per cloud agent. The queryable members are columns (<c>id</c>, <c>owner_id</c>, <c>session_id</c>,
/// <c>status</c>, <c>created_at</c>, <c>updated_at</c>, <c>idempotency_key</c>); everything else is the record as JSON in <c>record</c>.
/// The row never holds a secret value: a record names its secrets and nothing more. Unique indexes enforce one agent per session and one
/// agent per <c>(owner, Idempotency-Key)</c>.
/// </para>
/// <para>
/// This is migration 1 of the cloud agent store. A later change adds a statement to this batch guarded the same way (<c>ADD COLUMN IF NOT EXISTS</c>),
/// so a database created by any earlier version converges to the same shape.
/// </para>
/// </remarks>
public static class PostgresCloudAgentSchema
{
    private static readonly long SchemaLockKey = PostgresSql.AdvisoryKey("ompos.cloud-agents.schema");

    private const string CreateSchemaSql = "CREATE SCHEMA IF NOT EXISTS ompos";

    private const string CreateCloudAgentsSql = """
        CREATE TABLE IF NOT EXISTS cloud_agents (
            id text PRIMARY KEY,
            owner_id text NOT NULL,
            session_id text NOT NULL,
            status text NOT NULL,
            created_at timestamptz NOT NULL,
            updated_at timestamptz NOT NULL,
            idempotency_key text NULL,
            record jsonb NOT NULL
        )
        """;

    private const string CreateSessionIndexSql =
        "CREATE UNIQUE INDEX IF NOT EXISTS cloud_agents_session_idx ON cloud_agents (session_id)";

    private const string CreateIdempotencyIndexSql =
        "CREATE UNIQUE INDEX IF NOT EXISTS cloud_agents_owner_key_idx ON cloud_agents (owner_id, idempotency_key) WHERE idempotency_key IS NOT NULL";

    private const string CreateStatusIndexSql =
        "CREATE INDEX IF NOT EXISTS cloud_agents_status_updated_at_idx ON cloud_agents (status, updated_at)";

    private const string CreateOwnerIndexSql =
        "CREATE INDEX IF NOT EXISTS cloud_agents_owner_created_at_idx ON cloud_agents (owner_id, created_at DESC)";

    /// <summary>
    /// Creates the schema if it does not exist. Safe to call repeatedly and from several processes at once.
    /// </summary>
    /// <param name="dataSource">The data source for the target database. Its search path decides the schema of the table.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the schema exists.</returns>
    public static async Task EnsureCreatedAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                using var lockCommand = PostgresSql.Create(connection, transaction);
                lockCommand.CommandText = "SELECT pg_advisory_xact_lock(@key)";
                lockCommand.Parameters.AddWithValue("key", SchemaLockKey);
                await lockCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                using var command = PostgresSql.Create(connection, transaction);
                command.CommandText = CreateSchemaSql;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                command.CommandText = CreateCloudAgentsSql;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                command.CommandText = CreateSessionIndexSql;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                command.CommandText = CreateIdempotencyIndexSql;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                command.CommandText = CreateStatusIndexSql;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                command.CommandText = CreateOwnerIndexSql;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
