namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// M0014: puts the account erasure fence of <see cref="M0012_AccountErasureFence"/> on <c>cowork_memory_idempotency</c>, which M0013 creates after M0012. Without it a
/// memory create claim could be written for an erasing or erased account. Down removes only the trigger, and refuses while the fence holds any row, so that a rollback that starts at the head is refused before it drops anything.
/// </summary>
internal sealed class M0014_MemoryIdempotencyFence : IMigrationStep
{
    private const string Up = """
        CREATE OR REPLACE TRIGGER cowork_memory_idempotency_account_fence BEFORE INSERT OR UPDATE ON cowork_memory_idempotency FOR EACH ROW EXECUTE FUNCTION autonogrammer_fence_account();
        """;

    private const string Down = """
        DO $refuse$
        BEGIN
            IF EXISTS (SELECT 1 FROM account_erasure_fence) THEN
                RAISE EXCEPTION 'refusing to remove the memory claim fence: the account erasure fence holds erasure records, and without this trigger a claim could be written for an erased account' USING ERRCODE = 'AE005';
            END IF;
        END
        $refuse$;
        DROP TRIGGER IF EXISTS cowork_memory_idempotency_account_fence ON cowork_memory_idempotency;
        """;

    /// <inheritdoc />
    public int Version => 14;

    /// <inheritdoc />
    public string Name => "memory_idempotency_fence";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;
}
