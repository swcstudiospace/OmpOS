namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// M0016: the Postgres-backed automation store. <c>automations</c> holds one row per automation: the owner is a column, the definition is JSON in <c>record</c>,
/// and the schedule cursor is a column. <c>automation_runs</c> holds the run history; <c>seq</c> orders it, so the newest entry is the highest. Both tables carry the
/// owner, and both get the same erasure fence as the other account-owned tables (the M0014 function), so a writer that bypasses <c>AccountAdmission</c> still cannot
/// write for an account being erased. No column holds a secret value. Additive only: two tables, their indexes and two triggers; no existing table is altered.
/// Down drops the triggers and then the tables, and requires quiescence.
/// </summary>
internal sealed class M0016_AutomationStore : IMigrationStep
{
    private const string Up = """
        CREATE TABLE IF NOT EXISTS automations (
            id text PRIMARY KEY,
            owner_id text NOT NULL,
            record jsonb NOT NULL,
            schedule_cursor timestamptz NULL
        );
        CREATE INDEX IF NOT EXISTS automations_owner_idx ON automations (owner_id);

        CREATE TABLE IF NOT EXISTS automation_runs (
            seq bigint GENERATED ALWAYS AS IDENTITY,
            automation_id text NOT NULL REFERENCES automations (id) ON DELETE CASCADE,
            owner_id text NOT NULL,
            run_id text NOT NULL,
            record jsonb NOT NULL,
            PRIMARY KEY (automation_id, run_id)
        );
        CREATE UNIQUE INDEX IF NOT EXISTS automation_runs_seq_idx ON automation_runs (automation_id, seq);

        CREATE OR REPLACE TRIGGER automations_refuse_erasing BEFORE INSERT OR UPDATE ON automations FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'owner_id');

        CREATE OR REPLACE TRIGGER automation_runs_refuse_erasing BEFORE INSERT OR UPDATE ON automation_runs FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'owner_id');
        """;

    private const string Down = """
        DROP TRIGGER IF EXISTS automation_runs_refuse_erasing ON automation_runs;
        DROP TRIGGER IF EXISTS automations_refuse_erasing ON automations;
        DROP TABLE IF EXISTS automation_runs;
        DROP TABLE IF EXISTS automations;
        """;

    /// <inheritdoc />
    public int Version => 16;

    /// <inheritdoc />
    public string Name => "automation_store";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
