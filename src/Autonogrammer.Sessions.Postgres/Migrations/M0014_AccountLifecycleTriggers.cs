namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// M0014: database-side erasure fence. A single function refuses any insert or update of an account-owned row whose account is erasing, erased, or has no lifecycle row,
/// so a writer that bypasses <c>AccountAdmission</c> still cannot write to an account being erased. The check takes <c>FOR SHARE</c> on the lifecycle row, the same lock
/// <c>AccountAdmission.AdmitSharedAsync</c> takes, so erasure waits on in-flight writers. Deletes are not guarded: erasure removes rows with ordinary deletes.
/// Additive only: triggers and one function, no table is altered. Up uses <c>CREATE OR REPLACE TRIGGER</c>, so it is idempotent without a <c>DROP</c>; Down drops every trigger and then the function.
/// </summary>
internal sealed class M0014_AccountLifecycleTriggers : IMigrationStep
{
    private const string Up = """
        CREATE OR REPLACE FUNCTION autonogrammer_refuse_erasing_account()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $fence$
        DECLARE
            v_row jsonb;
            v_account text;
            v_state text;
        BEGIN
            v_row := to_jsonb(NEW);
            IF TG_ARGV[0] = 'session' THEN
                SELECT s.owner_id INTO v_account FROM sessions AS s WHERE s.id = v_row ->> TG_ARGV[1];
            ELSE
                v_account := v_row ->> TG_ARGV[1];
            END IF;

            SELECT l.state INTO v_state FROM account_lifecycle AS l WHERE l.account_id = v_account FOR SHARE;
            IF v_state IS NULL OR v_state IN ('erasing', 'erased') THEN
                RAISE EXCEPTION 'account % is erasing, erased, or not provisioned', v_account USING ERRCODE = 'AE001';
            END IF;
            RETURN NEW;
        END;
        $fence$;

        CREATE OR REPLACE TRIGGER accounts_refuse_erasing BEFORE INSERT OR UPDATE ON accounts FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'id');

        CREATE OR REPLACE TRIGGER sessions_refuse_erasing BEFORE INSERT OR UPDATE ON sessions FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'owner_id');

        CREATE OR REPLACE TRIGGER session_events_refuse_erasing BEFORE INSERT OR UPDATE ON session_events FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('session', 'session_id');

        CREATE OR REPLACE TRIGGER session_tool_approvals_refuse_erasing BEFORE INSERT OR UPDATE ON session_tool_approvals FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('session', 'session_id');

        CREATE OR REPLACE TRIGGER cloud_agents_refuse_erasing BEFORE INSERT OR UPDATE ON cloud_agents FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'owner_id');

        CREATE OR REPLACE TRIGGER dispatch_devices_refuse_erasing BEFORE INSERT OR UPDATE ON dispatch_devices FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'account_id');

        CREATE OR REPLACE TRIGGER dispatch_commands_refuse_erasing BEFORE INSERT OR UPDATE ON dispatch_commands FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'account_id');

        CREATE OR REPLACE TRIGGER dispatch_events_refuse_erasing BEFORE INSERT OR UPDATE ON dispatch_events FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'account_id');

        CREATE OR REPLACE TRIGGER cowork_events_refuse_erasing BEFORE INSERT OR UPDATE ON cowork_events FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'account_id');

        CREATE OR REPLACE TRIGGER cowork_memory_idempotency_refuse_erasing BEFORE INSERT OR UPDATE ON cowork_memory_idempotency FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'account_id');

        CREATE OR REPLACE TRIGGER cowork_memory_entries_refuse_erasing BEFORE INSERT OR UPDATE ON cowork_memory_entries FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'account_id');

        CREATE OR REPLACE TRIGGER cowork_memory_settings_refuse_erasing BEFORE INSERT OR UPDATE ON cowork_memory_settings FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'account_id');

        CREATE OR REPLACE TRIGGER cowork_instructions_refuse_erasing BEFORE INSERT OR UPDATE ON cowork_instructions FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'account_id');

        CREATE OR REPLACE TRIGGER cowork_projects_refuse_erasing BEFORE INSERT OR UPDATE ON cowork_projects FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'account_id');

        CREATE OR REPLACE TRIGGER artifacts_refuse_erasing BEFORE INSERT OR UPDATE ON artifacts FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'account_id');

        CREATE OR REPLACE TRIGGER artifact_versions_refuse_erasing BEFORE INSERT OR UPDATE ON artifact_versions FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'account_id');

        CREATE OR REPLACE TRIGGER artifact_grants_refuse_erasing BEFORE INSERT OR UPDATE ON artifact_grants FOR EACH ROW EXECUTE FUNCTION autonogrammer_refuse_erasing_account('account', 'account_id');
        """;

    private const string Down = """
        DROP TRIGGER IF EXISTS artifact_grants_refuse_erasing ON artifact_grants;
        DROP TRIGGER IF EXISTS artifact_versions_refuse_erasing ON artifact_versions;
        DROP TRIGGER IF EXISTS artifacts_refuse_erasing ON artifacts;
        DROP TRIGGER IF EXISTS cowork_projects_refuse_erasing ON cowork_projects;
        DROP TRIGGER IF EXISTS cowork_instructions_refuse_erasing ON cowork_instructions;
        DROP TRIGGER IF EXISTS cowork_memory_settings_refuse_erasing ON cowork_memory_settings;
        DROP TRIGGER IF EXISTS cowork_memory_entries_refuse_erasing ON cowork_memory_entries;
        DROP TRIGGER IF EXISTS cowork_memory_idempotency_refuse_erasing ON cowork_memory_idempotency;
        DROP TRIGGER IF EXISTS cowork_events_refuse_erasing ON cowork_events;
        DROP TRIGGER IF EXISTS dispatch_events_refuse_erasing ON dispatch_events;
        DROP TRIGGER IF EXISTS dispatch_commands_refuse_erasing ON dispatch_commands;
        DROP TRIGGER IF EXISTS dispatch_devices_refuse_erasing ON dispatch_devices;
        DROP TRIGGER IF EXISTS cloud_agents_refuse_erasing ON cloud_agents;
        DROP TRIGGER IF EXISTS session_tool_approvals_refuse_erasing ON session_tool_approvals;
        DROP TRIGGER IF EXISTS session_events_refuse_erasing ON session_events;
        DROP TRIGGER IF EXISTS sessions_refuse_erasing ON sessions;
        DROP TRIGGER IF EXISTS accounts_refuse_erasing ON accounts;
        DROP FUNCTION IF EXISTS autonogrammer_refuse_erasing_account();
        """;

    /// <inheritdoc />
    public int Version => 14;

    /// <inheritdoc />
    public string Name => "account_lifecycle_triggers";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
