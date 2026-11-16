namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// M0012: the durable per-account erasure fence (SEC-77-07). <c>account_erasure_fence</c> holds one permanent row per account that was or is being erased, keyed by a
/// SHA-256 digest of the account identifier, never by the identifier itself. A row stays after the erasure completes, so the account cannot be written again.
/// </summary>
/// <remarks>
/// <para>
/// Every table that holds account-owned rows carries a <c>BEFORE INSERT OR UPDATE</c> trigger that calls <c>autonogrammer_account_fence_check</c>. The check takes a shared,
/// transaction-scoped advisory lock on the account and refuses the write (SQLSTATE <c>AE001</c>) when the fence holds a row for it. The eraser takes the same lock exclusively
/// before it inserts the fence row, so it waits for every writer that was already admitted, commits the row, and from then on every writer finds it. Because the lock is
/// held to the end of the writer's own transaction, a writer that started before the fence committed cannot commit a row after the erasure ran. The check and the session
/// owner lookup run as the function owner (the migration role) with the search path of the migration, so a runtime role that has grants on one table only is still fenced.
/// </para>
/// <para>
/// The gate is in the database, not in the stores, so it covers every writer: the session, cloud agent, dispatch, memory, project and artifact stores, the audit
/// writer, direct SQL and an older binary. A row whose owner cannot be resolved is refused (<c>AE002</c>), and so is a transaction that does not run at READ COMMITTED
/// (<c>AE003</c>): the check must see the fence row that committed while it waited for the lock. Deletes are not gated; the eraser needs them.
/// </para>
/// <para>
/// <c>dispatch_audit</c> and <c>ledger_outbox</c> are not gated: they hold the keyed tenant hash and payload digests only, are append-only and are retained by design.
/// <c>cowork_memory_idempotency</c> is created by a later step and is gated by M0014. Down refuses while the fence holds any row, so a rollback can never reopen an erased account.
/// </para>
/// </remarks>
internal sealed class M0012_AccountErasureFence : IMigrationStep
{
    private const string Up = """
        CREATE TABLE IF NOT EXISTS account_erasure_fence (
            account_digest bytea PRIMARY KEY CHECK (octet_length(account_digest) = 32),
            state text NOT NULL CHECK (state IN ('erasing', 'erased')),
            started_at timestamptz NOT NULL DEFAULT now(),
            completed_at timestamptz NULL,
            CHECK ((state = 'erased') = (completed_at IS NOT NULL))
        );

        CREATE OR REPLACE FUNCTION autonogrammer_account_digest(p_account text)
        RETURNS bytea
        LANGUAGE sql
        IMMUTABLE STRICT
        AS $digest$
            SELECT sha256(convert_to('autonogrammer.account_erasure:' || p_account, 'UTF8'))
        $digest$;

        CREATE OR REPLACE FUNCTION autonogrammer_account_lock_key(p_account text)
        RETURNS bigint
        LANGUAGE sql
        IMMUTABLE STRICT
        AS $lockkey$
            SELECT hashtextextended('autonogrammer.account_erasure:' || p_account, 0)
        $lockkey$;

        CREATE OR REPLACE FUNCTION autonogrammer_account_fence_check(p_account text)
        RETURNS void
        LANGUAGE plpgsql
        SECURITY DEFINER
        SET search_path FROM CURRENT
        AS $check$
        BEGIN
            IF p_account IS NULL THEN
                RAISE EXCEPTION 'the account that owns this row cannot be resolved' USING ERRCODE = 'AE002';
            END IF;
            IF current_setting('transaction_isolation') <> 'read committed' THEN
                RAISE EXCEPTION 'account writes require the read committed isolation level' USING ERRCODE = 'AE003';
            END IF;
            PERFORM pg_advisory_xact_lock_shared(autonogrammer_account_lock_key(p_account));
            IF EXISTS (SELECT 1 FROM account_erasure_fence WHERE account_digest = autonogrammer_account_digest(p_account)) THEN
                RAISE EXCEPTION 'the account is being erased or has been erased' USING ERRCODE = 'AE001';
            END IF;
        END;
        $check$;

        CREATE OR REPLACE FUNCTION autonogrammer_fence_account()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $byaccount$
        BEGIN
            PERFORM autonogrammer_account_fence_check(NEW.account_id);
            RETURN NEW;
        END;
        $byaccount$;

        CREATE OR REPLACE FUNCTION autonogrammer_fence_owner()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $byowner$
        BEGIN
            PERFORM autonogrammer_account_fence_check(NEW.owner_id);
            RETURN NEW;
        END;
        $byowner$;

        CREATE OR REPLACE FUNCTION autonogrammer_fence_account_key()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $bykey$
        BEGIN
            PERFORM autonogrammer_account_fence_check(NEW.id);
            RETURN NEW;
        END;
        $bykey$;

        CREATE OR REPLACE FUNCTION autonogrammer_fence_session()
        RETURNS trigger
        LANGUAGE plpgsql
        SECURITY DEFINER
        SET search_path FROM CURRENT
        AS $bysession$
        BEGIN
            PERFORM autonogrammer_account_fence_check((SELECT s.owner_id FROM sessions AS s WHERE s.id = NEW.session_id));
            RETURN NEW;
        END;
        $bysession$;

        CREATE OR REPLACE FUNCTION autonogrammer_fence_row_guard()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $guard$
        BEGIN
            IF TG_OP = 'UPDATE' AND OLD.state = 'erasing' AND NEW.state = 'erased'
                AND NEW.account_digest = OLD.account_digest AND NEW.started_at = OLD.started_at THEN
                RETURN NEW;
            END IF;
            RAISE EXCEPTION 'an erasure fence row is permanent; it only moves from erasing to erased' USING ERRCODE = 'AE004';
        END;
        $guard$;

        CREATE OR REPLACE TRIGGER account_erasure_fence_row_guard BEFORE UPDATE OR DELETE ON account_erasure_fence FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_row_guard();

        CREATE OR REPLACE TRIGGER accounts_account_fence BEFORE INSERT OR UPDATE ON accounts FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_account_key();
        CREATE OR REPLACE TRIGGER sessions_account_fence BEFORE INSERT OR UPDATE ON sessions FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_owner();
        CREATE OR REPLACE TRIGGER session_events_account_fence BEFORE INSERT OR UPDATE ON session_events FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_session();
        CREATE OR REPLACE TRIGGER session_tool_approvals_account_fence BEFORE INSERT OR UPDATE ON session_tool_approvals FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_session();
        CREATE OR REPLACE TRIGGER cloud_agents_account_fence BEFORE INSERT OR UPDATE ON cloud_agents FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_owner();
        CREATE OR REPLACE TRIGGER dispatch_devices_account_fence BEFORE INSERT OR UPDATE ON dispatch_devices FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_account();
        CREATE OR REPLACE TRIGGER dispatch_commands_account_fence BEFORE INSERT OR UPDATE ON dispatch_commands FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_account();
        CREATE OR REPLACE TRIGGER dispatch_events_account_fence BEFORE INSERT OR UPDATE ON dispatch_events FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_account();
        CREATE OR REPLACE TRIGGER cowork_events_account_fence BEFORE INSERT OR UPDATE ON cowork_events FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_account();
        CREATE OR REPLACE TRIGGER artifacts_account_fence BEFORE INSERT OR UPDATE ON artifacts FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_account();
        CREATE OR REPLACE TRIGGER artifact_versions_account_fence BEFORE INSERT OR UPDATE ON artifact_versions FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_account();
        CREATE OR REPLACE TRIGGER artifact_grants_account_fence BEFORE INSERT OR UPDATE ON artifact_grants FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_account();
        CREATE OR REPLACE TRIGGER cowork_projects_account_fence BEFORE INSERT OR UPDATE ON cowork_projects FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_account();
        CREATE OR REPLACE TRIGGER cowork_instructions_account_fence BEFORE INSERT OR UPDATE ON cowork_instructions FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_account();
        CREATE OR REPLACE TRIGGER cowork_memory_entries_account_fence BEFORE INSERT OR UPDATE ON cowork_memory_entries FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_account();
        CREATE OR REPLACE TRIGGER cowork_memory_settings_account_fence BEFORE INSERT OR UPDATE ON cowork_memory_settings FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_account();
        """;

    private const string Down = """
        DO $refuse$
        BEGIN
            IF EXISTS (SELECT 1 FROM account_erasure_fence) THEN
                RAISE EXCEPTION 'refusing to roll back the account erasure fence: it holds erasure records that a rollback would lose and that keep erased accounts closed' USING ERRCODE = 'AE005';
            END IF;
        END
        $refuse$;
        DROP TRIGGER IF EXISTS accounts_account_fence ON accounts;
        DROP TRIGGER IF EXISTS sessions_account_fence ON sessions;
        DROP TRIGGER IF EXISTS session_events_account_fence ON session_events;
        DROP TRIGGER IF EXISTS session_tool_approvals_account_fence ON session_tool_approvals;
        DROP TRIGGER IF EXISTS cloud_agents_account_fence ON cloud_agents;
        DROP TRIGGER IF EXISTS dispatch_devices_account_fence ON dispatch_devices;
        DROP TRIGGER IF EXISTS dispatch_commands_account_fence ON dispatch_commands;
        DROP TRIGGER IF EXISTS dispatch_events_account_fence ON dispatch_events;
        DROP TRIGGER IF EXISTS cowork_events_account_fence ON cowork_events;
        DROP TRIGGER IF EXISTS artifacts_account_fence ON artifacts;
        DROP TRIGGER IF EXISTS artifact_versions_account_fence ON artifact_versions;
        DROP TRIGGER IF EXISTS artifact_grants_account_fence ON artifact_grants;
        DROP TRIGGER IF EXISTS cowork_projects_account_fence ON cowork_projects;
        DROP TRIGGER IF EXISTS cowork_instructions_account_fence ON cowork_instructions;
        DROP TRIGGER IF EXISTS cowork_memory_entries_account_fence ON cowork_memory_entries;
        DROP TRIGGER IF EXISTS cowork_memory_settings_account_fence ON cowork_memory_settings;
        DROP TRIGGER IF EXISTS account_erasure_fence_row_guard ON account_erasure_fence;
        DROP FUNCTION IF EXISTS autonogrammer_fence_row_guard();
        DROP FUNCTION IF EXISTS autonogrammer_fence_session();
        DROP FUNCTION IF EXISTS autonogrammer_fence_account_key();
        DROP FUNCTION IF EXISTS autonogrammer_fence_owner();
        DROP FUNCTION IF EXISTS autonogrammer_fence_account();
        DROP FUNCTION IF EXISTS autonogrammer_account_fence_check(text);
        DROP FUNCTION IF EXISTS autonogrammer_account_lock_key(text);
        DROP FUNCTION IF EXISTS autonogrammer_account_digest(text);
        DROP TABLE IF EXISTS account_erasure_fence;
        """;

    /// <inheritdoc />
    public int Version => 12;

    /// <inheritdoc />
    public string Name => "account_erasure_fence";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
