namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>M0003: the surface and the closed flag of a session, and the index that lists one owner's sessions newest first. <c>surface</c> holds the lower-case name of a <c>SessionSurface</c> and has no CHECK constraint, so a new surface is additive; the API validates it.</summary>
internal sealed class M0003_SessionSurfaceOwner : IMigrationStep
{
    private const string Up = """
        ALTER TABLE sessions ADD COLUMN IF NOT EXISTS surface text NOT NULL DEFAULT 'code';
        ALTER TABLE sessions ADD COLUMN IF NOT EXISTS closed_at timestamptz NULL;
        CREATE INDEX IF NOT EXISTS sessions_owner_updated_idx ON sessions (owner_id, updated_at DESC);
        """;

    private const string Down = """
        DROP INDEX IF EXISTS sessions_owner_updated_idx;
        ALTER TABLE sessions DROP COLUMN IF EXISTS closed_at;
        ALTER TABLE sessions DROP COLUMN IF EXISTS surface;
        """;

    /// <inheritdoc />
    public int Version => 3;

    /// <inheritdoc />
    public string Name => "session_surface_owner";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
