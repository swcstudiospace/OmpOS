namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// Adds the account-to-tenant binding table. The ledger outbox holds only the keyed tenant hash of an account, so a writer that holds a tenant hash must be able to
/// resolve it to the account whose lifecycle admission fences the write. Each account has one binding, and each tenant hash belongs to one account.
/// Additive only: one new table, no existing table is altered. Down drops the table and requires quiescence.
/// </summary>
internal sealed class M0015_AccountTenantBindings : IMigrationStep
{
    private const string Up = """
        CREATE TABLE IF NOT EXISTS account_tenant_bindings (
            account_id text PRIMARY KEY,
            tenant_hash text NOT NULL UNIQUE
        );
        """;

    private const string Down = "DROP TABLE IF EXISTS account_tenant_bindings;";

    /// <inheritdoc />
    public int Version => 15;

    /// <inheritdoc />
    public string Name => "account_tenant_bindings";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
