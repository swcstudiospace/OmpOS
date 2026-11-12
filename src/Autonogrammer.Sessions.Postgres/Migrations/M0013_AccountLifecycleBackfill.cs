namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>Backfills an active lifecycle row for every account that predates the lifecycle table, so the erasure admission fence finds a row for each existing account. Additive and idempotent: existing lifecycle rows are left untouched. Down removes only the rows this step could have created: active accounts still at erasure epoch 0.</summary>
internal sealed class M0013_AccountLifecycleBackfill : IMigrationStep
{
    private const string Up = """
        INSERT INTO account_lifecycle (account_id)
        SELECT id FROM accounts
        ON CONFLICT (account_id) DO NOTHING;
        """;

    private const string Down = """
        DELETE FROM account_lifecycle
        WHERE state = 'active' AND erasure_epoch = 0;
        """;

    /// <inheritdoc />
    public int Version => 13;

    /// <inheritdoc />
    public string Name => "account_lifecycle_backfill";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
