namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>Adds the per-account lifecycle row that gates erasure. Rows are created on first write, so no backfill is needed. Additive only: no existing table is altered. Down drops the lifecycle state and requires quiescence.</summary>
internal sealed class M0012_AccountLifecycle : IMigrationStep
{
    private const string Up = """
        CREATE TABLE IF NOT EXISTS account_lifecycle (
            account_id text PRIMARY KEY,
            state text NOT NULL DEFAULT 'active' CONSTRAINT account_lifecycle_state_ck CHECK (state IN ('active', 'erasing', 'erased')),
            erasure_epoch bigint NOT NULL DEFAULT 0,
            updated_at timestamptz NOT NULL DEFAULT now()
        );
        """;

    private const string Down = "DROP TABLE IF EXISTS account_lifecycle;";

    /// <inheritdoc />
    public int Version => 12;

    /// <inheritdoc />
    public string Name => "account_lifecycle";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
