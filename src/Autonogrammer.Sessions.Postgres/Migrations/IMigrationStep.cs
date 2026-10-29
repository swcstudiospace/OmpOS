namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// One numbered, reversible schema change. The <see cref="MigrationRunner"/> applies the steps of <see cref="MigrationRegistry.All"/> in order up to the
/// compiled head, each in one transaction, and records the SHA-256 of <see cref="UpSql"/> so a step that changed after it was applied is refused.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="UpSql"/> is additive: it creates objects with <c>IF NOT EXISTS</c> and adds columns with <c>ADD COLUMN IF NOT EXISTS</c>. It never removes an
/// object, never changes a column type, never creates an extension and never removes a schema; a <c>NOT NULL</c> column added to an existing table has a
/// constant default. A guard test scans every registered step for these rules. The SQL names tables without a schema: the runner puts the target schema
/// first in the search path of the transaction.
/// </para>
/// <para>
/// <see cref="DownSql"/> removes only what <see cref="UpSql"/> created. It runs only when an operator asks for it (<c>migrate --down</c>), never automatically
/// and never in the release that shipped the step.
/// </para>
/// </remarks>
public interface IMigrationStep
{
    /// <summary>Gets the version number. Versions are unique and strictly increase along <see cref="MigrationRegistry.All"/>.</summary>
    int Version { get; }

    /// <summary>Gets the short snake-case name recorded in <c>schema_migrations</c>, for example <c>session_store</c>.</summary>
    string Name { get; }

    /// <summary>Gets the additive SQL that applies the step. Its SHA-256 is the step's checksum.</summary>
    string UpSql { get; }

    /// <summary>Gets the SQL that reverts the step.</summary>
    string DownSql { get; }
}
