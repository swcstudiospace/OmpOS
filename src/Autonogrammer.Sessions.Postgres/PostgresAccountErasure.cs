using Autonogrammer.Resilience;
using Npgsql;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// Erases everything an account owns from PostgreSQL, and then from the places the caller names, so the deletion of an account leaves nothing that identifies it
/// except the append-only audit rows, which carry only the keyed tenant hash.
/// </summary>
/// <remarks>
/// <para>
/// Erasure is lifecycle-first. In one committed transaction it takes the account's <c>account_lifecycle</c> row exclusively, marks the account
/// <c>erasing</c>, deletes the account's automation runs and automations, and deletes the account's tenant binding (<see cref="AccountTenantBindings"/>), so the keyed tenant hash no longer resolves to the account. That transaction waits for every writer that holds the shared admission (<see cref="AccountAdmission"/>) to commit, and every writer that
/// starts after it is refused. Only then are the families deleted, each in one transaction keyed by the owner: the dispatch tables (commands, feed events,
/// devices); <c>cowork_events</c>; the memory family (entries, settings, instructions, projects); the artifacts (grants, versions, artifacts); the session
/// content (events and tool approvals through the owner's session ids, then the sessions); <c>cloud_agents</c>; and the <c>accounts</c> row. The deletes are
/// hard deletes. Last, the lifecycle row becomes <c>erased</c>. <c>dispatch_audit</c> is not touched: its rows hold the keyed tenant hash and no account
/// identifier, and the runtime role has no right to change them anyway.
/// </para>
/// <para>
/// A lock wait longer than the lock timeout fails with <see cref="AccountErasureLockTimeoutException"/> and changes nothing. A failure after the account
/// was marked <c>erasing</c> leaves it <c>erasing</c>, and the call can simply be repeated: every family delete is idempotent. An account that is already
/// <c>erased</c> skips the families. The tenant-key delegate runs once, after the last transaction committed, and is not retried here.
/// </para>
/// <para>
/// This class does not reference the Dragonfly project (both are layer-2 libraries). The composition root binds <c>eraseTenantKeys</c> to the tenant key eraser.
/// </para>
/// </remarks>
public sealed class PostgresAccountErasure
{
    private const string SetLockTimeoutSql = "SET LOCAL lock_timeout = '30s'";
    private const string EnsureLifecycleRowSql = "INSERT INTO account_lifecycle (account_id) VALUES (@account_id) ON CONFLICT (account_id) DO NOTHING";
    private const string LockLifecycleRowSql = "SELECT state FROM account_lifecycle WHERE account_id = @account_id FOR UPDATE";
    private const string BeginErasureSql =
        "UPDATE account_lifecycle SET state = 'erasing', erasure_epoch = erasure_epoch + 1, updated_at = now() WHERE account_id = @account_id";
    private const string MarkErasedSql = "UPDATE account_lifecycle SET state = 'erased', updated_at = now() WHERE account_id = @account_id";
    private const string LockNotAvailableSqlState = "55P03";
    private const string ErasedState = "erased";

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

    private const string DeleteAutomationRunsSql = "DELETE FROM automation_runs WHERE owner_id = @account_id";
    private const string DeleteAutomationsSql = "DELETE FROM automations WHERE owner_id = @account_id";

    private const string DeleteAccountSql = "DELETE FROM accounts WHERE id = @account_id";

    private readonly NpgsqlDataSource _dataSource;
    private readonly ResiliencePipeline _pipeline;

    /// <summary>Initializes a new instance of the <see cref="PostgresAccountErasure"/> class.</summary>
    /// <param name="dataSource">The data source of the runtime role. Its lifetime is owned by the caller.</param>
    /// <param name="resilience">The pipeline every command runs through, or <see langword="null"/> for a private pipeline with the default options.</param>
    public PostgresAccountErasure(NpgsqlDataSource dataSource, PostgresResilience? resilience = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
        _pipeline = (resilience ?? PostgresResilience.CreateDefault()).Pipeline;
    }

    /// <summary>Deletes everything <paramref name="accountId"/> owns, then awaits <paramref name="eraseTenantKeys"/> once.</summary>
    /// <param name="accountId">The account identifier: the subject of the identity provider, which is the <c>account_id</c> and <c>owner_id</c> of its rows.</param>
    /// <param name="tenantHash">The keyed tenant hash of the account, handed to <paramref name="eraseTenantKeys"/> unchanged.</param>
    /// <param name="eraseTenantKeys">Deletes the tenant's keys outside PostgreSQL (the Dragonfly keyspace). It receives <paramref name="tenantHash"/> and runs after the last transaction committed.</param>
    /// <param name="cancellationToken">Cancels the erasure between and during the transactions.</param>
    /// <returns>A task that completes when every family and the tenant keys are deleted.</returns>
    /// <exception cref="AccountErasureLockTimeoutException">Writers still held the account's lifecycle lock after the lock timeout. Nothing was changed.</exception>
    public async Task EraseAsync(string accountId, string tenantHash, Func<string, CancellationToken, Task> eraseTenantKeys, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantHash);
        ArgumentNullException.ThrowIfNull(eraseTenantKeys);

        if (await BeginErasureAsync(accountId, cancellationToken).ConfigureAwait(false))
        {
            await InTransactionAsync(accountId, EraseDispatchAsync, cancellationToken).ConfigureAwait(false);
            await InTransactionAsync(accountId, EraseCoworkEventsAsync, cancellationToken).ConfigureAwait(false);
            await InTransactionAsync(accountId, EraseMemoryAsync, cancellationToken).ConfigureAwait(false);
            await InTransactionAsync(accountId, EraseArtifactsAsync, cancellationToken).ConfigureAwait(false);
            await InTransactionAsync(accountId, EraseSessionContentAsync, cancellationToken).ConfigureAwait(false);
            await InTransactionAsync(accountId, EraseCloudAgentsAsync, cancellationToken).ConfigureAwait(false);
            await InTransactionAsync(accountId, EraseAccountRowAsync, cancellationToken).ConfigureAwait(false);
            await InTransactionAsync(accountId, MarkErasedAsync, cancellationToken).ConfigureAwait(false);
        }

        await eraseTenantKeys(tenantHash, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes the exclusive lifecycle lock of the account and marks it <c>erasing</c>, in one committed transaction, so writers are refused from this point on.
    /// The lock wait is bounded by <c>lock_timeout</c>; a timeout is reported as <see cref="AccountErasureLockTimeoutException"/> and changes nothing.
    /// </summary>
    /// <returns><see langword="true"/> when the families must still be deleted; <see langword="false"/> when the account is already <c>erased</c>.</returns>
    private Task<bool> BeginErasureAsync(string accountId, CancellationToken cancellationToken) =>
        _pipeline.ExecuteTransactionAsync(
            OperationClass.Background,
            async token =>
            {
                var connection = await _dataSource.OpenConnectionAsync(token).ConfigureAwait(false);
                await using (connection.ConfigureAwait(false))
                {
                    using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
                    try
                    {
                        return await LockAndMarkErasingAsync(connection, transaction, accountId, token).ConfigureAwait(false);
                    }
                    catch (PostgresException exception) when (string.Equals(exception.SqlState, LockNotAvailableSqlState, StringComparison.Ordinal))
                    {
                        throw new AccountErasureLockTimeoutException(
                            $"Account '{accountId}' is busy: in-flight writers held its lifecycle lock past the erasure lock timeout. Nothing was erased; repeat the erasure.",
                            exception);
                    }
                }
            },
            cancellationToken);

    private static async Task<bool> LockAndMarkErasingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string accountId,
        CancellationToken cancellationToken)
    {
        using (var timeout = PostgresSql.Create(connection, transaction))
        {
            timeout.CommandText = SetLockTimeoutSql;
            await timeout.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        using var lifecycle = PostgresSql.Create(connection, transaction);
        lifecycle.Parameters.AddWithValue("account_id", accountId);
        lifecycle.CommandText = EnsureLifecycleRowSql;
        await lifecycle.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        lifecycle.CommandText = LockLifecycleRowSql;
        var state = await lifecycle.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string
            ?? throw new InvalidOperationException($"Account '{accountId}' has no lifecycle row after it was ensured.");
        await AccountTenantBindings.DeleteAsync(connection, transaction, accountId, cancellationToken).ConfigureAwait(false);
        var needsErasure = !string.Equals(state, ErasedState, StringComparison.Ordinal);
        if (needsErasure)
        {
            lifecycle.CommandText = BeginErasureSql;
            await lifecycle.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await EraseAutomationsAsync(lifecycle, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return needsErasure;
    }

    private static async Task EraseAutomationsAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        command.CommandText = DeleteAutomationRunsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        command.CommandText = DeleteAutomationsSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

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

    private static async Task EraseAccountRowAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        command.CommandText = DeleteAccountSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task MarkErasedAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        command.CommandText = MarkErasedSql;
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
                    using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
                    using var command = PostgresSql.Create(connection, transaction);
                    command.Parameters.AddWithValue("account_id", accountId);
                    await family(command, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return true;
                }
            },
            cancellationToken);
}
