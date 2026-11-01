namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// M0010: device-row state the hub needs after pairing. Every column is added with <c>IF NOT EXISTS</c>, and the composite owner key is a
/// unique index that already exists on a second apply. <c>last_source_prefix</c> stays on the device row and is never copied to the audit table.
/// </summary>
internal sealed class M0010_DispatchDeviceState : IMigrationStep
{
    private const string Up = """
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS credential_jti text;
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS credential_expires_at timestamptz;
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS capabilities jsonb;
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS app_version text;
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS protocol text;
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS revoke_reason text;
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS paired_session_id text;
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS last_source_prefix text;
        CREATE UNIQUE INDEX IF NOT EXISTS dispatch_devices_id_account_uq ON dispatch_devices (id, account_id);
        """;

    private const string Down = """
        DROP INDEX IF EXISTS dispatch_devices_id_account_uq;
        ALTER TABLE dispatch_devices DROP COLUMN IF EXISTS last_source_prefix;
        ALTER TABLE dispatch_devices DROP COLUMN IF EXISTS paired_session_id;
        ALTER TABLE dispatch_devices DROP COLUMN IF EXISTS revoke_reason;
        ALTER TABLE dispatch_devices DROP COLUMN IF EXISTS protocol;
        ALTER TABLE dispatch_devices DROP COLUMN IF EXISTS app_version;
        ALTER TABLE dispatch_devices DROP COLUMN IF EXISTS capabilities;
        ALTER TABLE dispatch_devices DROP COLUMN IF EXISTS credential_expires_at;
        ALTER TABLE dispatch_devices DROP COLUMN IF EXISTS credential_jti;
        """;

    /// <inheritdoc />
    public int Version => 10;

    /// <inheritdoc />
    public string Name => "dispatch_device_state";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
