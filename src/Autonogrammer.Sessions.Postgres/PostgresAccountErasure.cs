using Autonogrammer.Resilience;
using Npgsql;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// Erases everything an account owns from PostgreSQL, and then from the places the caller names, so the deletion of an account leaves nothing that identifies it
/// except the append-only audit rows, which carry only the keyed tenant hash.
/// </summary>
/// <remarks>
/// <para>
/// The families are deleted in this order, each in one transaction keyed by the owner: the dispatch tables (commands, feed events, devices); <c>cowork_events</c>;
/// the memory family (entries, settings, instructions, projects); the artifacts (grants, versions, artifacts); the session content (events and tool approvals
/// through the owner's session ids, then the sessions); <c>cloud_agents</c>; and last the <c>accounts</c> row. The deletes are hard deletes. <c>dispatch_audit</c> is
/// not touched: its rows hold the keyed tenant hash and no account identifier, and the runtime role has no right to change them anyway.
/// </para>
/// <para>
/// A failure leaves the families that already committed deleted and the account row in place, and the call can simply be repeated: every family delete is
/// idempotent. The tenant-key delegate runs once, after the last transaction committed, and is not retried here.
/// </para>
/// <para>Callers must disable the account and quiesce all writers before erasure. The family transactions do not prevent concurrent account-wide writes.</para>
/// <para>
/// This class does not reference the Dragonfly project (both are layer-2 libraries). The composition root binds <c>eraseTenantKeys</c> to the tenant key eraser.
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
    public async Task EraseAsync(string accountId, string tenantHash, Func<string, CancellationToken, Task> eraseTenantKeys, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantHash);
        ArgumentNullException.ThrowIfNull(eraseTenantKeys);

        await InTransactionAsync(accountId, EraseDispatchAsync, cancellationToken).ConfigureAwait(false);
        await InTransactionAsync(accountId, EraseCoworkEventsAsync, cancellationToken).ConfigureAwait(false);
        await InTransactionAsync(accountId, EraseMemoryAsync, cancellationToken).ConfigureAwait(false);
        await InTransactionAsync(accountId, EraseArtifactsAsync, cancellationToken).ConfigureAwait(false);
        await InTransactionAsync(accountId, EraseSessionContentAsync, cancellationToken).ConfigureAwait(false);
        await InTransactionAsync(accountId, EraseCloudAgentsAsync, cancellationToken).ConfigureAwait(false);
        await InTransactionAsync(accountId, EraseAccountRowAsync, cancellationToken).ConfigureAwait(false);

        await eraseTenantKeys(tenantHash, cancellationToken).ConfigureAwait(false);
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
