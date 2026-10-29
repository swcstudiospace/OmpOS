namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// M0009: Cowork projects, the account-wide instructions and the memory tables. All are user content, owner-scoped by <c>account_id</c> in every query, with no
/// row-level security. A memory entry that belongs to a project is removed with the project. The memory search is a full-text search on <c>topic</c> and
/// <c>content</c> (the <c>simple</c> configuration, so no language is assumed); the settings default to off, so a row that omits a flag never switches memory on.
/// </summary>
internal sealed class M0009_CoworkProjectsMemory : IMigrationStep
{
    private const string Up = """
        CREATE TABLE IF NOT EXISTS cowork_projects (
            id text PRIMARY KEY,
            account_id text NOT NULL,
            name text NOT NULL,
            description text NULL,
            instructions text NULL,
            context_urls jsonb NOT NULL DEFAULT '[]',
            use_account_memory boolean NOT NULL DEFAULT false,
            archived_at timestamptz NULL,
            created_at timestamptz NOT NULL,
            updated_at timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS cowork_projects_account_idx ON cowork_projects (account_id, archived_at, updated_at DESC);
        CREATE TABLE IF NOT EXISTS cowork_instructions (
            account_id text PRIMARY KEY,
            body text NOT NULL,
            updated_at timestamptz NOT NULL
        );
        CREATE TABLE IF NOT EXISTS cowork_memory_entries (
            id text PRIMARY KEY,
            account_id text NOT NULL,
            project_id text NULL REFERENCES cowork_projects (id) ON DELETE CASCADE,
            topic text NULL,
            content text NOT NULL,
            category text NULL,
            source_session text NULL,
            created_at timestamptz NOT NULL,
            updated_at timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS cowork_memory_entries_account_idx ON cowork_memory_entries (account_id, project_id, updated_at DESC);
        CREATE INDEX IF NOT EXISTS cowork_memory_entries_search_idx ON cowork_memory_entries
            USING gin (to_tsvector('simple', coalesce(topic, '') || ' ' || coalesce(content, '')));
        CREATE TABLE IF NOT EXISTS cowork_memory_settings (
            account_id text PRIMARY KEY,
            enabled boolean NOT NULL DEFAULT false,
            paused boolean NOT NULL DEFAULT false,
            include_sensitive boolean NOT NULL DEFAULT false
        );
        """;

    private const string Down = """
        DROP TABLE IF EXISTS cowork_memory_settings;
        DROP TABLE IF EXISTS cowork_memory_entries;
        DROP TABLE IF EXISTS cowork_instructions;
        DROP TABLE IF EXISTS cowork_projects;
        """;

    /// <inheritdoc />
    public int Version => 9;

    /// <inheritdoc />
    public string Name => "cowork_projects_memory";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
