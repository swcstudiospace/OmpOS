using Npgsql;

namespace OmpOS.Sessions.Postgres;

/// <summary>
/// Creates the PostgreSQL tables behind <see cref="PostgresSessionRepository"/>. Creation is idempotent: every
/// statement is <c>CREATE TABLE IF NOT EXISTS</c>, and the whole batch runs under a transaction-scoped advisory lock
/// so that concurrent starts of the API and the worker serialize instead of racing on the catalog.
/// </summary>
/// <remarks>
/// <para>
/// The tables are <c>sessions</c> (one header row per session), <c>session_events</c> (the append-only stream log,
/// primary key <c>(session_id, seq)</c>) and <c>session_tool_approvals</c> (one row per tool-approval request, primary
/// key <c>(session_id, request_id)</c>). Nothing in this store updates or deletes an event row.
/// </para>
/// </remarks>
public static class PostgresSessionSchema
{
    private static readonly long SchemaLockKey = PostgresSql.AdvisoryKey("ompos.sessions.schema");

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

    private const string CreateSessionEventsSql = """
        CREATE TABLE IF NOT EXISTS session_events (
            session_id text NOT NULL REFERENCES sessions (id),
            seq bigint NOT NULL,
            correlation_id text NOT NULL,
            event_timestamp timestamptz NOT NULL,
            payload jsonb NOT NULL,
            PRIMARY KEY (session_id, seq)
        )
        """;

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

    /// <summary>Creates the session tables if they do not exist. Safe to call repeatedly and from several processes at once.</summary>
    /// <param name="dataSource">The data source for the target database.</param>
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
                await ExecuteAsync(lockCommand, cancellationToken).ConfigureAwait(false);

                using var sessionsCommand = PostgresSql.Create(connection, transaction);
                sessionsCommand.CommandText = CreateSessionsSql;
                await ExecuteAsync(sessionsCommand, cancellationToken).ConfigureAwait(false);

                using var eventsCommand = PostgresSql.Create(connection, transaction);
                eventsCommand.CommandText = CreateSessionEventsSql;
                await ExecuteAsync(eventsCommand, cancellationToken).ConfigureAwait(false);

                using var approvalsCommand = PostgresSql.Create(connection, transaction);
                approvalsCommand.CommandText = CreateSessionToolApprovalsSql;
                await ExecuteAsync(approvalsCommand, cancellationToken).ConfigureAwait(false);

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
