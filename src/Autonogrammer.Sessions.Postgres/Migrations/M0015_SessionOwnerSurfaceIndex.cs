namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// M0015: indexes the session listing by owner and surface. <c>ListByOwnerAsync</c> filters <c>owner_id</c> and <c>surface</c> and pages newest first on
/// <c>updated_at</c>, but the only owner index is <c>sessions_owner_updated_idx (owner_id, updated_at)</c>: the surface equality is filtered after the index
/// scan, so every Code/Cowork page pays for rows of the other surface. This step adds <c>sessions_owner_surface_updated_idx (owner_id, surface, updated_at)</c>
/// so the equality and the order come from the same scan. It is additive: one <c>IF NOT EXISTS</c> index, no column, no type change.
/// The index build is transaction-local with <c>lock_timeout</c> 5 s, the same <c>set_config(..., true)</c> idiom the account erasure uses, because the runner
/// applies every step in one transaction where <c>CREATE INDEX CONCURRENTLY</c> cannot run. Down drops only this index.
/// </summary>
internal sealed class M0015_SessionOwnerSurfaceIndex : IMigrationStep
{
    private const string Up = """
        SELECT set_config('lock_timeout', '5000', true);
        CREATE INDEX IF NOT EXISTS sessions_owner_surface_updated_idx ON sessions (owner_id, surface, updated_at DESC);
        """;

    private const string Down = """
        DROP INDEX IF EXISTS sessions_owner_surface_updated_idx;
        """;

    /// <inheritdoc />
    public int Version => 15;

    /// <inheritdoc />
    public string Name => "session_owner_surface";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
