namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>M0001: the session store as it existed before migrations: the sessions table, the event log hypertable and the tool approvals, moved unchanged from the code that created them at startup. The event log is partitioned on <c>occurred_at</c> with one-day chunks, so its primary key is <c>(session_id, seq, occurred_at)</c>; the session header row lock, not the database, keeps Seq unique within a session.</summary>
internal sealed class M0001_SessionStore : IMigrationStep
{
    private const string Up = """
        CREATE TABLE IF NOT EXISTS sessions (
            id text PRIMARY KEY,
            owner_id text NOT NULL,
            created_at timestamptz NOT NULL,
            updated_at timestamptz NOT NULL,
            status text NOT NULL,
            sandbox_id text NULL,
            messages jsonb NOT NULL
        );
        CREATE INDEX IF NOT EXISTS sessions_status_updated_at_idx ON sessions (status, updated_at);
        CREATE TABLE IF NOT EXISTS session_events (
            session_id text NOT NULL REFERENCES sessions (id),
            seq bigint NOT NULL,
            correlation_id text NOT NULL,
            occurred_at timestamptz NOT NULL,
            payload jsonb NOT NULL,
            PRIMARY KEY (session_id, seq, occurred_at)
        );
        SELECT create_hypertable('session_events', 'occurred_at', chunk_time_interval => INTERVAL '1 day', if_not_exists => TRUE);
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
        );
        """;

    private const string Down = """
        DROP TABLE IF EXISTS session_tool_approvals;
        DROP TABLE IF EXISTS session_events;
        DROP TABLE IF EXISTS sessions;
        """;

    /// <inheritdoc />
    public int Version => 1;

    /// <inheritdoc />
    public string Name => "session_store";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
