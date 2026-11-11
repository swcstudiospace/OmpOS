namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>Adds durable, account-scoped memory create claims. Down loses replay history and requires quiescence.</summary>
internal sealed class M0011_MemoryIdempotency : IMigrationStep
{
    private const string Up = """
        CREATE TABLE IF NOT EXISTS cowork_memory_idempotency (
            account_id text NOT NULL,
            operation text NOT NULL CHECK (operation = 'create'),
            key varchar(128) NOT NULL CHECK (length(key) BETWEEN 1 AND 128),
            fingerprint_version integer NOT NULL,
            fingerprint bytea NOT NULL CHECK (octet_length(fingerprint) = 32),
            entry_id text NOT NULL,
            created_at timestamptz NOT NULL,
            expires_at timestamptz NOT NULL,
            PRIMARY KEY (account_id, operation, key)
        );
        CREATE INDEX IF NOT EXISTS cowork_memory_idempotency_expires_idx ON cowork_memory_idempotency (expires_at);
        """;

    private const string Down = "DROP TABLE IF EXISTS cowork_memory_idempotency;";

    /// <inheritdoc />
    public int Version => 11;

    /// <inheritdoc />
    public string Name => "memory_idempotency";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
