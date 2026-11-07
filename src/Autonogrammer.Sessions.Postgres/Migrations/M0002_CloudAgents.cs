namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>M0002: the cloud agent table and its four indexes, moved unchanged from the code that created them at startup. The queryable members are columns; the rest of the record is JSON in <c>record</c>, and no column holds a secret value.</summary>
internal sealed class M0002_CloudAgents : IMigrationStep
{
    private const string Up = """
        CREATE TABLE IF NOT EXISTS cloud_agents (
            id text PRIMARY KEY,
            owner_id text NOT NULL,
            session_id text NOT NULL,
            status text NOT NULL,
            created_at timestamptz NOT NULL,
            updated_at timestamptz NOT NULL,
            idempotency_key text NULL,
            record jsonb NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS cloud_agents_session_idx ON cloud_agents (session_id);
        CREATE UNIQUE INDEX IF NOT EXISTS cloud_agents_owner_key_idx ON cloud_agents (owner_id, idempotency_key) WHERE idempotency_key IS NOT NULL;
        CREATE INDEX IF NOT EXISTS cloud_agents_status_updated_at_idx ON cloud_agents (status, updated_at);
        CREATE INDEX IF NOT EXISTS cloud_agents_owner_created_at_idx ON cloud_agents (owner_id, created_at DESC);
        """;

    private const string Down = """
        DROP TABLE IF EXISTS cloud_agents;
        """;

    /// <inheritdoc />
    public int Version => 2;

    /// <inheritdoc />
    public string Name => "cloud_agents";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
