namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>M0004: the API's own account row, keyed by the identity provider's subject. It holds no email and no profile data: the email of an account response comes from the token. <c>feed_seq</c> is the per-account counter that numbers the account's feed; a writer increments it under the row lock, so a reader that resumes from <c>id &gt; last</c> never skips a late commit.</summary>
internal sealed class M0004_Accounts : IMigrationStep
{
    private const string Up = """
        CREATE TABLE IF NOT EXISTS accounts (
            id text PRIMARY KEY,
            created_at timestamptz NOT NULL,
            last_seen_at timestamptz NOT NULL,
            status text NOT NULL DEFAULT 'active',
            deleted_at timestamptz NULL,
            feed_seq bigint NOT NULL DEFAULT 0
        );
        """;

    private const string Down = """
        DROP TABLE IF EXISTS accounts;
        """;

    /// <inheritdoc />
    public int Version => 4;

    /// <inheritdoc />
    public string Name => "accounts";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
