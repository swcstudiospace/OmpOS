using Npgsql;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// Creates the PostgreSQL schema behind <see cref="PostgresSessionRepository"/>: the <c>autonogrammer</c> schema, the TimescaleDB
/// extension, the session tables and the hypertable that holds the event log. Creation is idempotent: every statement is
/// guarded by <c>IF NOT EXISTS</c>, and the whole batch runs under a transaction-scoped advisory lock so that concurrent
/// starts of the API and any other process serialize instead of racing on the catalog.
/// </summary>
/// <remarks>
/// <para>
/// Table names are unqualified. They resolve through the search path of the connection, which the data source sets to
/// <c>autonogrammer,public</c> (see <see cref="ServiceCollectionExtensions"/>), so the tables are created in <c>autonogrammer</c>.
/// </para>
/// <para>
/// The tables are <c>sessions</c> (one header row per session, with an index on <c>(status, updated_at)</c> for
/// <see cref="ISessionRepository.ListSessionIdsByStatusAsync"/>), <c>session_events</c> (the append-only stream log) and
/// <c>session_tool_approvals</c> (one row per tool-approval request, primary key <c>(session_id, request_id)</c>).
/// Nothing in this store updates or deletes an event row.
/// </para>
/// <para>
/// <c>session_events</c> is a TimescaleDB hypertable partitioned on <c>occurred_at</c> with one-day chunks. A hypertable's
/// primary key must include the partitioning column, so the primary key is <c>(session_id, seq, occurred_at)</c>, and the
/// database does not by itself stop two events of one session from sharing a Seq. That guarantee comes from the session
/// header row lock: every writer takes that lock before it chooses a Seq. See <see cref="PostgresSessionRepository"/>.
/// </para>
/// </remarks>
public static class PostgresSessionSchema
{
    private static readonly long SchemaLockKey = PostgresSql.AdvisoryKey("autonogrammer.sessions.schema");

    private const string CreateSchemaSql = "CREATE SCHEMA IF NOT EXISTS autonogrammer";

    private const string CreateTimescaleExtensionSql = "CREATE EXTENSION IF NOT EXISTS timescaledb WITH SCHEMA public";

    private const string CreateSessionsSql = """
        CREATE TABLE IF NOT EXISTS sessions (
            id text PRIMARY KEY,
            owner_id text NOT NULL,
            created_at timestamptz NOT NULL,
            updated_at timestamptz NOT NULL,
            status text NOT NULL,
            sandbox_id text NULL,
            messages jsonb NOT NULL
        )
        """;

    private const string CreateSessionsStatusIndexSql =
        "CREATE INDEX IF NOT EXISTS sessions_status_updated_at_idx ON sessions (status, updated_at)";

    private const string CreateSessionEventsSql = """
        CREATE TABLE IF NOT EXISTS session_events (
            session_id text NOT NULL REFERENCES sessions (id),
            seq bigint NOT NULL,
            correlation_id text NOT NULL,
            occurred_at timestamptz NOT NULL,
            payload jsonb NOT NULL,
            PRIMARY KEY (session_id, seq, occurred_at)
        )
        """;

    // Unqualified on purpose: the name resolves through the search path to the table CREATE TABLE just made, in autonogrammer in production.
    private const string CreateSessionEventsHypertableSql =
        "SELECT create_hypertable('session_events', 'occurred_at', chunk_time_interval => INTERVAL '1 day', if_not_exists => TRUE)";

    private const string CreateSessionToolApprovalsSql = """
        CREATE TABLE IF NOT EXISTS session_tool_approvals (
            session_id text NOT NULL REFERENCES sessions (id),
            request_id text NOT NULL,
            tool_name text NOT NULL,
            granted_tools jsonb NOT NULL,
            requested_at timestamptz NOT NULL,
            decision text NULL CHECK (decision IN ('Allow', 'Deny')),
            decided_at timestamptz NULL,
            PRIMARY KEY (session_id, request_id),
            CHECK ((decision IS NULL) = (decided_at IS NULL))
        )
        """;


    /// <summary>
    /// Creates the schema if it does not exist. Safe to call repeatedly and from several processes at once: the batch runs
    /// under a transaction-scoped advisory lock, and every statement is idempotent.
    /// </summary>
    /// <param name="dataSource">The data source for the target database. Its search path decides the schema of the tables.</param>
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
                // The lock comes first: CREATE SCHEMA and CREATE EXTENSION race in the catalog when two processes run them at once.
                using var lockCommand = PostgresSql.Create(connection, transaction);
                lockCommand.CommandText = "SELECT pg_advisory_xact_lock(@key)";
                lockCommand.Parameters.AddWithValue("key", SchemaLockKey);
                await ExecuteAsync(lockCommand, cancellationToken).ConfigureAwait(false);

                using var command = PostgresSql.Create(connection, transaction);
                command.CommandText = CreateSchemaSql;
                await ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
                command.CommandText = CreateTimescaleExtensionSql;
                await ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
                command.CommandText = CreateSessionsSql;
                await ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
                command.CommandText = CreateSessionsStatusIndexSql;
                await ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
                command.CommandText = CreateSessionEventsSql;
                await ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
                command.CommandText = CreateSessionEventsHypertableSql;
                await ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
                command.CommandText = CreateSessionToolApprovalsSql;
                await ExecuteAsync(command, cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Runs a non-query command. The caller owns the command and disposes it.</summary>
    /// <param name="command">The command to run.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    private static async Task ExecuteAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
