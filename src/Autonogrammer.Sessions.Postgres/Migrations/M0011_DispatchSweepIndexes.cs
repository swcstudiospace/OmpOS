namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// M0011: retain command receipt time and index the sweep's active states. Existing in-flight rows have no recorded receipt time;
/// their current state-entry timestamp is the best available approximation when this migration is applied.
/// </summary>
internal sealed class M0011_DispatchSweepIndexes : IMigrationStep
{
    private const string Up = """
        ALTER TABLE dispatch_commands ADD COLUMN IF NOT EXISTS received_at timestamptz;
        UPDATE dispatch_commands SET received_at = updated_at WHERE received_at IS NULL AND state IN ('received', 'awaiting_approval', 'accepted', 'running');
        CREATE INDEX IF NOT EXISTS dispatch_commands_sweep_received_idx ON dispatch_commands ((coalesce(received_at, updated_at))) WHERE state IN ('received', 'accepted', 'running');
        CREATE INDEX IF NOT EXISTS dispatch_commands_sweep_updated_idx ON dispatch_commands (updated_at) WHERE state IN ('queued', 'delivered', 'received', 'awaiting_approval', 'accepted', 'running');
        CREATE INDEX IF NOT EXISTS dispatch_commands_sweep_expiry_idx ON dispatch_commands (expires_at) WHERE state IN ('queued', 'delivered');
        """;

    private const string Down = """
        DROP INDEX IF EXISTS dispatch_commands_sweep_received_idx;
        DROP INDEX IF EXISTS dispatch_commands_sweep_expiry_idx;
        DROP INDEX IF EXISTS dispatch_commands_sweep_updated_idx;
        ALTER TABLE dispatch_commands DROP COLUMN IF EXISTS received_at;
        """;

    /// <inheritdoc />
    public int Version => 11;

    /// <inheritdoc />
    public string Name => "dispatch_sweep_indexes";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
