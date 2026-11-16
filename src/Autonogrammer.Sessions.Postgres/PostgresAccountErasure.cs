using Autonogrammer.Resilience;
using Npgsql;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// Erases everything an account owns from PostgreSQL, and then from the places the caller names, so the deletion of an account leaves nothing that identifies it
/// except the append-only audit rows, which carry only the keyed tenant hash, and the erasure fence row, which carries only a digest of the identifier.
/// </summary>
/// <remarks>
/// <para>
/// The erasure runs behind the durable fence of <c>M0012_AccountErasureFence</c>. Step one is its own committed transaction: it takes the account's advisory lock
/// exclusively, which waits for every writer that was already admitted to commit or roll back, and inserts the fence row in state <c>erasing</c>. From the moment that
/// transaction commits the database refuses every insert and update of an account-owned row for the account (SQLSTATE <c>AE001</c>, surfaced by the stores as
/// <see cref="AccountErasingException"/>), whatever writer sends it. Step two deletes the families, each in one transaction keyed by the owner: the dispatch tables
/// (commands, feed events, devices); <c>cowork_events</c>; the memory family (claims, entries, settings, instructions, projects); the artifacts (grants, versions, artifacts);
/// the session content (events and tool approvals through the owner's session ids, then the sessions); and <c>cloud_agents</c>. Step three, in one transaction, deletes the
/// <c>accounts</c> row and moves the fence row to <c>erased</c>. The deletes are hard deletes. <c>dispatch_audit</c> and <c>ledger_outbox</c> are not touched: they hold the keyed
/// tenant hash and payload digests and no account identifier, and the runtime role has no right to change the audit anyway. The fence row is permanent, so the account
/// cannot be written again after the erasure.
/// </para>
/// <para>
/// A failure leaves the families that already committed deleted, the fence in place and the account row in place, and the call can simply be repeated: every family delete is
/// idempotent and the fence is only ever inserted once. A call for an account whose fence already says <c>erased</c> skips the SQL and only runs the tenant-key delegate again,
/// so a failed key deletion is retried and the call completes only when the keys are deleted. The delegate runs after the last transaction committed.
/// </para>
/// <para>
/// The Dragonfly keyspace is outside the fence: its keys are coordination state with a lifetime of at most 24 hours, and the tenant-key scan removes the ones that carry the tenant
/// segment. This class does not reference the Dragonfly project (both are layer-2 libraries). The composition root binds <c>eraseTenantKeys</c> to the tenant key eraser.
/// </para>
/// </remarks>
public sealed class PostgresAccountErasure
{
    private const string DeleteDispatchCommandsSql = "DELETE FROM dispatch_commands WHERE account_id = @account_id";
    private const string DeleteDispatchEventsSql = "DELETE FROM dispatch_events WHERE account_id = @account_id";
    private const string DeleteDispatchDevicesSql = "DELETE FROM dispatch_devices WHERE account_id = @account_id";

    private const string DeleteCoworkEventsSql = "DELETE FROM cowork_events WHERE account_id = @account_id";

    private const string DeleteMemoryClaimsSql = "DELETE FROM cowork_memory_idempotency WHERE account_id = @account_id";
    private const string DeleteMemoryEntriesSql = "DELETE FROM cowork_memory_entries WHERE account_id = @account_id";
    private const string DeleteMemorySettingsSql = "DELETE FROM cowork_memory_settings WHERE account_id = @account_id";
    private const string DeleteInstructionsSql = "DELETE FROM cowork_instructions WHERE account_id = @account_id";
    private const string DeleteProjectsSql = "DELETE FROM cowork_projects WHERE account_id = @account_id";

    private const string DeleteArtifactGrantsSql = "DELETE FROM artifact_grants WHERE account_id = @account_id";
    private const string DeleteArtifactVersionsSql = "DELETE FROM artifact_versions WHERE account_id = @account_id";
    private const string DeleteArtifactsSql = "DELETE FROM artifacts WHERE account_id = @account_id";

    private const string DeleteSessionEventsSql =
        "DELETE FROM session_events WHERE session_id IN (SELECT id FROM sessions WHERE owner_id = @account_id)";

    private const string DeleteSessionApprovalsSql =
        "DELETE FROM session_tool_approvals WHERE session_id IN (SELECT id FROM sessions WHERE owner_id = @account_id)";

    private const string DeleteSessionsSql = "DELETE FROM sessions WHERE owner_id = @account_id";

    private const string DeleteCloudAgentsSql = "DELETE FROM cloud_agents WHERE owner_id = @account_id";

    private const string DeleteAccountSql = "DELETE FROM accounts WHERE id = @account_id";

    // The fence step runs in its own transaction: the shared lock of every admitted writer is waited for with the lock timeout, then the row is inserted once.
    private const string SetLockTimeoutSql = "SELECT set_config('lock_timeout', @lock_timeout, true)";
    private const string LockAccountSql = "SELECT pg_advisory_xact_lock(autonogrammer_account_lock_key(@account_id))";
    private const string InsertFenceSql =
        "INSERT INTO account_erasure_fence (account_digest, state) VALUES (autonogrammer_account_digest(@account_id), 'erasing') ON CONFLICT (account_digest) DO NOTHING";

    private const string ReadFenceStateSql = "SELECT state FROM account_erasure_fence WHERE account_digest = autonogrammer_account_digest(@account_id)";

    private const string CompleteFenceSql =
        "UPDATE account_erasure_fence SET state = 'erased', completed_at = now() WHERE account_digest = autonogrammer_account_digest(@account_id) AND state = 'erasing'";

    private const string ErasedState = "erased";

    // 55P03: lock_not_available, raised when lock_timeout expires.
    private const string LockNotAvailableSqlState = "55P03";

    /// <summary>The default time the fence step waits for writers that were already admitted.</summary>
    public static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromSeconds(20);

    private readonly NpgsqlDataSource _dataSource;
    private readonly ResiliencePipeline _pipeline;
    private readonly TimeSpan _lockTimeout;

    /// <summary>Initializes a new instance of the <see cref="PostgresAccountErasure"/> class.</summary>
    /// <param name="dataSource">The data source of the runtime role. Its lifetime is owned by the caller.</param>
    /// <param name="resilience">The pipeline every command runs through, or <see langword="null"/> for a private pipeline with the default options.</param>
    /// <param name="lockTimeout">How long the fence step waits for writers that were already admitted, or <see langword="null"/> for <see cref="DefaultLockTimeout"/>. It must be shorter than the background command timeout of the pipeline.</param>
    public PostgresAccountErasure(NpgsqlDataSource dataSource, PostgresResilience? resilience = null, TimeSpan? lockTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
        _pipeline = (resilience ?? PostgresResilience.CreateDefault()).Pipeline;
        _lockTimeout = lockTimeout ?? DefaultLockTimeout;
        if (_lockTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lockTimeout), "The lock timeout must be positive.");
        }
    }

    /// <summary>Fences the account, deletes everything <paramref name="accountId"/> owns, then awaits <paramref name="eraseTenantKeys"/> once.</summary>
    /// <param name="accountId">The account identifier: the subject of the identity provider, which is the <c>account_id</c> and <c>owner_id</c> of its rows.</param>
    /// <param name="tenantHash">The keyed tenant hash of the account, handed to <paramref name="eraseTenantKeys"/> unchanged.</param>
    /// <param name="eraseTenantKeys">Deletes the tenant's keys outside PostgreSQL (the Dragonfly keyspace). It receives <paramref name="tenantHash"/> and runs after the last transaction committed, on every call that gets that far.</param>
    /// <param name="cancellationToken">Cancels the erasure between and during the transactions.</param>
    /// <returns>A task that completes when the fence is complete, every family and the tenant keys are deleted.</returns>
    /// <exception cref="AccountErasureBusyException">Writers that were already admitted held the account past the lock timeout. Nothing was changed; repeat the erasure.</exception>
    public async Task EraseAsync(string accountId, string tenantHash, Func<string, CancellationToken, Task> eraseTenantKeys, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantHash);
        ArgumentNullException.ThrowIfNull(eraseTenantKeys);

        if (await FenceAsync(accountId, cancellationToken).ConfigureAwait(false))
        {
            await InTransactionAsync(accountId, EraseDispatchAsync, cancellationToken).ConfigureAwait(false);
            await InTransactionAsync(accountId, EraseCoworkEventsAsync, cancellationToken).ConfigureAwait(false);
            await InTransactionAsync(accountId, EraseMemoryAsync, cancellationToken).ConfigureAwait(false);
            await InTransactionAsync(accountId, EraseArtifactsAsync, cancellationToken).ConfigureAwait(false);
            await InTransactionAsync(accountId, EraseSessionContentAsync, cancellationToken).ConfigureAwait(false);
            await InTransactionAsync(accountId, EraseCloudAgentsAsync, cancellationToken).ConfigureAwait(false);
            await InTransactionAsync(accountId, EraseAccountRowAndCompleteFenceAsync, cancellationToken).ConfigureAwait(false);
        }

        await eraseTenantKeys(tenantHash, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Step one, in its own transaction: waits for every admitted writer, then inserts the fence row once. The row commits before any delete runs, so a writer that starts
    /// afterwards is refused by the database, and a writer that started earlier has committed (or rolled back) before the lock was granted.
    /// </summary>
    /// <returns><see langword="true"/> when the families still have to be deleted; <see langword="false"/> when an earlier call already completed the erasure.</returns>
    private Task<bool> FenceAsync(string accountId, CancellationToken cancellationToken) =>
        _pipeline.ExecuteAsync(
            OperationClass.Background,
            true,
            async token =>
            {
                var connection = await _dataSource.OpenConnectionAsync(token).ConfigureAwait(false);
                await using (connection.ConfigureAwait(false))
                {
                    var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
                    await using (transaction.ConfigureAwait(false))
                    {
                        try
                        {
                            using var command = PostgresSql.Create(connection, transaction);
                            command.Parameters.AddWithValue("account_id", accountId);
                            command.Parameters.AddWithValue("lock_timeout", FormattableString.Invariant($"{(long)_lockTimeout.TotalMilliseconds}ms"));
                            command.CommandText = SetLockTimeoutSql;
                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                            command.CommandText = LockAccountSql;
                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                            command.CommandText = InsertFenceSql;
                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                            command.CommandText = ReadFenceStateSql;
                            var state = await command.ExecuteScalarAsync(token).ConfigureAwait(false) as string
                                ?? throw new InvalidOperationException("The erasure fence row is missing after it was inserted.");
                            await transaction.CommitAsync(token).ConfigureAwait(false);
                            return !string.Equals(state, ErasedState, StringComparison.Ordinal);
                        }
                        catch (PostgresException exception) when (string.Equals(exception.SqlState, LockNotAvailableSqlState, StringComparison.Ordinal))
                        {
                            throw new AccountErasureBusyException(
                                "Writers that were already admitted still hold the account. Nothing was erased; repeat the erasure.",
                                exception);
                        }
                    }
                }
            },
            cancellationToken);

    private static async Task EraseDispatchAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        command.CommandText = DeleteDispatchCommandsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        command.CommandText = DeleteDispatchEventsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        command.CommandText = DeleteDispatchDevicesSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EraseCoworkEventsAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        command.CommandText = DeleteCoworkEventsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EraseMemoryAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        command.CommandText = DeleteMemoryClaimsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        command.CommandText = DeleteMemoryEntriesSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        command.CommandText = DeleteMemorySettingsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        command.CommandText = DeleteInstructionsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        command.CommandText = DeleteProjectsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EraseArtifactsAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        command.CommandText = DeleteArtifactGrantsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        command.CommandText = DeleteArtifactVersionsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        command.CommandText = DeleteArtifactsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EraseSessionContentAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        command.CommandText = DeleteSessionEventsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        command.CommandText = DeleteSessionApprovalsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        command.CommandText = DeleteSessionsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EraseCloudAgentsAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        command.CommandText = DeleteCloudAgentsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EraseAccountRowAndCompleteFenceAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        command.CommandText = DeleteAccountSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        command.CommandText = CompleteFenceSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs one family in one transaction, as a background call that is safe to retry: every statement deletes by owner, so a second run finds nothing left.
    /// </summary>
    private Task<bool> InTransactionAsync(string accountId, Func<NpgsqlCommand, CancellationToken, Task> family, CancellationToken cancellationToken) =>
        _pipeline.ExecuteAsync(
            OperationClass.Background,
            true,
            async token =>
            {
                var connection = await _dataSource.OpenConnectionAsync(token).ConfigureAwait(false);
                await using (connection.ConfigureAwait(false))
                {
                    var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
                    await using (transaction.ConfigureAwait(false))
                    {
                        using var command = PostgresSql.Create(connection, transaction);
                        command.Parameters.AddWithValue("account_id", accountId);
                        await family(command, token).ConfigureAwait(false);
                        await transaction.CommitAsync(token).ConfigureAwait(false);
                        return true;
                    }
                }
            },
            cancellationToken);
}
