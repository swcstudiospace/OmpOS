namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// M0006: the Cowork event log and the artifact tables. A Cowork conversation stays in <c>session_events</c> (a Cowork session is a <c>sessions</c> row with
/// <c>surface = 'cowork'</c>); <c>cowork_events</c> holds the artifact and schedule events. Its payload is at most 64 KiB and its <c>kind</c> is the closed list
/// <c>artifact.created</c>, <c>artifact.updated</c>, <c>artifact.grant</c>, <c>artifact.revoke</c>, <c>schedule.run</c>, <c>schedule.late</c> and
/// <c>schedule.waiting_for_approval</c>; the writer enforces both, so a new kind or a new limit needs no migration. A version body is at most 5 MiB here; the
/// per-kind limits are the API's. Every table carries <c>account_id</c> and every query scopes by it: there is no row-level security.
/// </summary>
internal sealed class M0006_Cowork : IMigrationStep
{
    private const string Up = """
        CREATE TABLE IF NOT EXISTS cowork_events (
            account_id text NOT NULL,
            session_id text NOT NULL,
            seq bigint NOT NULL,
            occurred_at timestamptz NOT NULL,
            kind text NOT NULL,
            payload jsonb NOT NULL,
            blob_ref text NULL,
            PRIMARY KEY (session_id, seq, occurred_at)
        );
        SELECT create_hypertable('cowork_events', 'occurred_at', chunk_time_interval => INTERVAL '1 day', if_not_exists => TRUE);
        CREATE INDEX IF NOT EXISTS cowork_events_account_idx ON cowork_events (account_id, occurred_at DESC);
        CREATE TABLE IF NOT EXISTS artifacts (
            id text PRIMARY KEY,
            account_id text NOT NULL,
            title text NOT NULL,
            kind text NOT NULL,
            derived_from text NULL,
            latest_number int NOT NULL,
            created_at timestamptz NOT NULL,
            updated_at timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS artifacts_account_updated_idx ON artifacts (account_id, updated_at DESC);
        CREATE TABLE IF NOT EXISTS artifact_versions (
            artifact_id text NOT NULL REFERENCES artifacts (id) ON DELETE CASCADE,
            version int NOT NULL,
            session_id text NOT NULL,
            account_id text NOT NULL,
            content_sha256 text NOT NULL,
            size_bytes bigint NOT NULL,
            media_type text NOT NULL,
            parent_number int NULL,
            parent_content_sha256 text NULL,
            author text NOT NULL,
            change_note text NULL,
            permissions jsonb NOT NULL DEFAULT '[]',
            created_at timestamptz NOT NULL,
            content bytea NULL,
            PRIMARY KEY (artifact_id, version),
            CHECK (octet_length(content) <= 5242880)
        );
        CREATE INDEX IF NOT EXISTS artifact_versions_account_idx ON artifact_versions (account_id);
        CREATE TABLE IF NOT EXISTS artifact_grants (
            artifact_id text NOT NULL,
            version int NOT NULL,
            account_id text NOT NULL,
            content_sha256 text NOT NULL,
            origins jsonb NOT NULL,
            scope text NOT NULL,
            device text NOT NULL,
            granted_at timestamptz NOT NULL,
            PRIMARY KEY (artifact_id, version, scope, device),
            FOREIGN KEY (artifact_id, version) REFERENCES artifact_versions (artifact_id, version) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS artifact_grants_account_idx ON artifact_grants (account_id);
        """;

    private const string Down = """
        DROP TABLE IF EXISTS artifact_grants;
        DROP TABLE IF EXISTS artifact_versions;
        DROP TABLE IF EXISTS artifacts;
        DROP TABLE IF EXISTS cowork_events;
        """;

    /// <inheritdoc />
    public int Version => 6;

    /// <inheritdoc />
    public string Name => "cowork";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
