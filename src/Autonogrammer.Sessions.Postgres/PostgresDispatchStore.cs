using System.Globalization;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Resilience;
using Npgsql;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// PostgreSQL <see cref="IDispatchStore"/>. Sequence allocation and the queue cap run in the insert transaction under the device row lock.
/// Feed identifiers come from <c>accounts.feed_seq</c> under the account row lock. A foreign account matches no row.
/// </summary>
/// <remarks>
/// Command fingerprint, capability, issue time and failure reason live in <c>dispatch_commands.result</c> as a store document, because M0005 has
/// no columns for them. The document's <c>text</c> member is the result text. Both the body and that text are cleared seven days after a terminal
/// state (OQ-423 default). The audit insert is in the same transaction and never receives a body, name, prompt, token or address.
/// </remarks>
public sealed class PostgresDispatchStore : IDispatchStore
{
    private const string Platform = "unspecified";
    private const string Queued = "queued";
    private const string DeviceSelect =
        "SELECT id, account_id, name, public_key, coalesce(credential_jti, ''), coalesce(credential_expires_at::text, ''), coalesce(capabilities::text, '[]'), coalesce(paired_session_id, ''), coalesce(last_seen_at::text, ''), coalesce(revoked_at::text, ''), coalesce(revoke_reason, '') FROM dispatch_devices";

    private const string CommandSelect =
        "SELECT device_id, seq, op, state, idempotency_key, expires_at::text, (body IS NULL)::int, coalesce(body::text, ''), coalesce(result::text, ''), created_at::text, id FROM dispatch_commands";

    private readonly NpgsqlDataSource _dataSource;
    private readonly DispatchStoreLimits _limits;
    private readonly TimeProvider _timeProvider;
    private readonly Func<string, string> _tenantHash;
    private readonly ResiliencePipeline _pipeline;

    /// <summary>Initializes a new instance of the <see cref="PostgresDispatchStore"/> class.</summary>
    /// <param name="dataSource">The data source whose search path holds the dispatch tables.</param>
    /// <param name="limits">Caps and retention. The store does not read API options.</param>
    /// <param name="time">Clock for retention, expiry and audit times.</param>
    /// <param name="tenantHash">Keyed tenant hash. The composition root binds <c>ITenantHasher.HashTenant</c>.</param>
    /// <param name="resilience">The pipeline every command runs through, or <see langword="null"/> for the default options.</param>
    public PostgresDispatchStore(
        NpgsqlDataSource dataSource,
        DispatchStoreLimits limits,
        TimeProvider time,
        Func<string, string> tenantHash,
        PostgresResilience? resilience = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(tenantHash);
        if (limits.MaxQueuedPerDevice < 1 || limits.DevicesPerAccount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limits));
        }

        _dataSource = dataSource;
        _limits = limits;
        _timeProvider = time;
        _tenantHash = tenantHash;
        _pipeline = (resilience ?? PostgresResilience.CreateDefault()).Pipeline;
    }

    /// <inheritdoc />
    public ValueTask<DispatchDeviceCreateResult> CreateDeviceAsync(CreateDispatchDevice request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ValueTask<DispatchDeviceCreateResult>(_pipeline.ExecuteTransactionAsync(OperationClass.Interactive, token => CreateDeviceCoreAsync(request, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<DispatchDeviceRecord?> GetDeviceAsync(string accountId, string deviceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        return new ValueTask<DispatchDeviceRecord?>(_pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: true, token => GetDeviceCoreAsync(accountId, deviceId, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<DispatchDeviceRecord>> ListDevicesAsync(string accountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        return new ValueTask<IReadOnlyList<DispatchDeviceRecord>>(_pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: true, token => ListDevicesCoreAsync(accountId, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<bool> RenameDeviceAsync(string accountId, string deviceId, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(name);
        return new ValueTask<bool>(_pipeline.ExecuteTransactionAsync(OperationClass.Interactive, token => RenameCoreAsync(accountId, deviceId, name, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<bool> RevokeDeviceAsync(string accountId, string deviceId, string? reason, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        return new ValueTask<bool>(_pipeline.ExecuteTransactionAsync(OperationClass.Interactive, token => RevokeCoreAsync(accountId, deviceId, reason, revokedAt, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<bool> UpdateCredentialJtiAsync(string accountId, string deviceId, string credentialJti, DateTimeOffset credentialExpiresAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(credentialJti);
        return new ValueTask<bool>(_pipeline.ExecuteTransactionAsync(OperationClass.Interactive, token => CredentialCoreAsync(accountId, deviceId, credentialJti, credentialExpiresAt, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<bool> UpdateCapabilitySnapshotAsync(string accountId, string deviceId, IReadOnlyList<DispatchCapabilitySnapshot> capabilities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(capabilities);
        return new ValueTask<bool>(_pipeline.ExecuteTransactionAsync(OperationClass.Interactive, token => CapabilityCoreAsync(accountId, deviceId, capabilities, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<bool> TouchLastSeenAsync(string accountId, string deviceId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        return new ValueTask<bool>(_pipeline.ExecuteTransactionAsync(OperationClass.Interactive, token => TouchCoreAsync(accountId, deviceId, lastSeenAt, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<bool> UpdatePairedSessionAsync(string accountId, string deviceId, string pairedSessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(pairedSessionId);
        return new ValueTask<bool>(_pipeline.ExecuteTransactionAsync(OperationClass.Interactive, token => PairedCoreAsync(accountId, deviceId, pairedSessionId, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<DispatchEnqueueResult> EnqueueAsync(EnqueueDispatchCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ValueTask<DispatchEnqueueResult>(_pipeline.ExecuteTransactionAsync(OperationClass.Interactive, token => EnqueueCoreAsync(request, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<DispatchEnqueueResult?> ReplayAsync(EnqueueDispatchCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ValueTask<DispatchEnqueueResult?>(_pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: true, token => ReplayCoreAsync(request, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<DispatchCommandRecord?> GetCommandAsync(string accountId, string commandId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(commandId);
        return new ValueTask<DispatchCommandRecord?>(_pipeline.ExecuteTransactionAsync(OperationClass.Interactive, token => GetCommandCoreAsync(accountId, commandId, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<DispatchCommandRecord>> ListQueuedAfterAsync(string accountId, string deviceId, long afterSeq, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        return new ValueTask<IReadOnlyList<DispatchCommandRecord>>(_pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: true, token => ListQueuedCoreAsync(accountId, deviceId, afterSeq, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<DispatchCommandRecord>> ListCommandsAsync(string accountId, string? deviceId, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 100);
        return new ValueTask<IReadOnlyList<DispatchCommandRecord>>(_pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: true, token => ListCommandsCoreAsync(accountId, deviceId, limit, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<DispatchTransitionResult> TryTransitionAsync(string accountId, string commandId, DispatchCommandState expected, DispatchCommandState desired, DispatchFailureReason? reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(commandId);
        return new ValueTask<DispatchTransitionResult>(_pipeline.ExecuteTransactionAsync(OperationClass.Interactive, token => TransitionCoreAsync(accountId, commandId, expected, desired, reason, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<DispatchFeedEventRecord> AppendFeedEventAsync(string accountId, string kind, string? payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(kind);
        return new ValueTask<DispatchFeedEventRecord>(_pipeline.ExecuteTransactionAsync(OperationClass.Interactive, token => AppendFeedCoreAsync(accountId, kind, payload, token), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<DispatchFeedPage> ReadFeedAsync(string accountId, long afterId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        return new ValueTask<DispatchFeedPage>(_pipeline.ExecuteTransactionAsync(OperationClass.Interactive, token => ReadFeedCoreAsync(accountId, afterId, token), cancellationToken));
    }

    private async Task<DispatchDeviceCreateResult> CreateDeviceCoreAsync(CreateDispatchDevice request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await LockAccountAsync(connection, transaction, request.AccountId, now, cancellationToken).ConfigureAwait(false);
                if (await CountActiveDevicesAsync(connection, transaction, request.AccountId, cancellationToken).ConfigureAwait(false) >= _limits.DevicesPerAccount)
                {
                    return new DispatchDeviceCreateResult(DispatchDeviceCreateStatus.DeviceLimitReached, null);
                }

                if (await DeviceExistsAsync(connection, transaction, request.DeviceId, cancellationToken).ConfigureAwait(false))
                {
                    return new DispatchDeviceCreateResult(DispatchDeviceCreateStatus.DeviceIdTaken, null);
                }

                await InsertDeviceAsync(connection, transaction, request, now, cancellationToken).ConfigureAwait(false);
                await AuditAsync(connection, transaction, request.AccountId, request.DeviceId, null, "device.create", "created", cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new DispatchDeviceCreateResult(DispatchDeviceCreateStatus.Created, ToDevice(request));
            }
        }
    }

    private async Task<DispatchDeviceRecord?> GetDeviceCoreAsync(string accountId, string deviceId, CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            using var command = PostgresSql.Create(connection, null);
            command.CommandText = DeviceSelect + " WHERE id = @id AND account_id = @account_id";
            command.Parameters.AddWithValue("id", deviceId);
            command.Parameters.AddWithValue("account_id", accountId);
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadDevice(reader) : null;
            }
        }
    }

    private async Task<IReadOnlyList<DispatchDeviceRecord>> ListDevicesCoreAsync(string accountId, CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            using var command = PostgresSql.Create(connection, null);
            command.CommandText = DeviceSelect + " WHERE account_id = @account_id ORDER BY created_at, id";
            command.Parameters.AddWithValue("account_id", accountId);
            var list = new List<DispatchDeviceRecord>();
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    list.Add(ReadDevice(reader));
                }
            }

            return list;
        }
    }

    private async Task<bool> RenameCoreAsync(string accountId, string deviceId, string name, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                using var command = PostgresSql.Create(connection, transaction);
                command.CommandText = "UPDATE dispatch_devices SET name = @name WHERE id = @id AND account_id = @account_id";
                command.Parameters.AddWithValue("id", deviceId);
                command.Parameters.AddWithValue("account_id", accountId);
                command.Parameters.AddWithValue("name", name);
                return await FinishUpdateAsync(connection, transaction, command, accountId, deviceId, "device.rename", cancellationToken).ConfigureAwait(false);
            }
        }
    }
    private async Task<bool> RevokeCoreAsync(string accountId, string deviceId, string? reason, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                using var command = PostgresSql.Create(connection, transaction);
                command.CommandText = "UPDATE dispatch_devices SET revoked_at = @revoked_at, revoke_reason = @reason WHERE id = @id AND account_id = @account_id";
                command.Parameters.AddWithValue("id", deviceId);
                command.Parameters.AddWithValue("account_id", accountId);
                command.Parameters.AddWithValue("revoked_at", revokedAt);
                command.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
                return await FinishUpdateAsync(connection, transaction, command, accountId, deviceId, "device.revoke", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> CredentialCoreAsync(string accountId, string deviceId, string credentialJti, DateTimeOffset credentialExpiresAt, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                using var command = PostgresSql.Create(connection, transaction);
                command.CommandText = "UPDATE dispatch_devices SET credential_jti = @jti, credential_expires_at = @expires WHERE id = @id AND account_id = @account_id";
                command.Parameters.AddWithValue("id", deviceId);
                command.Parameters.AddWithValue("account_id", accountId);
                command.Parameters.AddWithValue("jti", credentialJti);
                command.Parameters.AddWithValue("expires", credentialExpiresAt);
                return await FinishUpdateAsync(connection, transaction, command, accountId, deviceId, "device.credential", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> CapabilityCoreAsync(string accountId, string deviceId, IReadOnlyList<DispatchCapabilitySnapshot> capabilities, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                using var command = PostgresSql.Create(connection, transaction);
                command.CommandText = "UPDATE dispatch_devices SET capabilities = CAST(@capabilities AS jsonb) WHERE id = @id AND account_id = @account_id";
                command.Parameters.AddWithValue("id", deviceId);
                command.Parameters.AddWithValue("account_id", accountId);
                command.Parameters.AddWithValue("capabilities", WriteCapabilities(capabilities));
                return await FinishUpdateAsync(connection, transaction, command, accountId, deviceId, "device.capabilities", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> TouchCoreAsync(string accountId, string deviceId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                using var command = PostgresSql.Create(connection, transaction);
                command.CommandText = "UPDATE dispatch_devices SET last_seen_at = @seen WHERE id = @id AND account_id = @account_id";
                command.Parameters.AddWithValue("id", deviceId);
                command.Parameters.AddWithValue("account_id", accountId);
                command.Parameters.AddWithValue("seen", lastSeenAt);
                return await FinishUpdateAsync(connection, transaction, command, accountId, deviceId, "device.touch", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> PairedCoreAsync(string accountId, string deviceId, string pairedSessionId, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                using var command = PostgresSql.Create(connection, transaction);
                command.CommandText = "UPDATE dispatch_devices SET paired_session_id = @session WHERE id = @id AND account_id = @account_id";
                command.Parameters.AddWithValue("id", deviceId);
                command.Parameters.AddWithValue("account_id", accountId);
                command.Parameters.AddWithValue("session", pairedSessionId);
                return await FinishUpdateAsync(connection, transaction, command, accountId, deviceId, "device.paired_session", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<DispatchEnqueueResult> EnqueueCoreAsync(EnqueueDispatchCommand request, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                if (!await LockLiveDeviceAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false))
                {
                    return new DispatchEnqueueResult(DispatchEnqueueStatus.NotFound, null);
                }

                var replay = await ReadIdempotentAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
                if (replay is not null)
                {
                    return replay;
                }

                if (await CountQueuedAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false) >= _limits.MaxQueuedPerDevice)
                {
                    return new DispatchEnqueueResult(DispatchEnqueueStatus.QueueFull, null);
                }

                var seq = await AllocateSeqAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
                await InsertCommandAsync(connection, transaction, request, seq, cancellationToken).ConfigureAwait(false);
                await AuditAsync(connection, transaction, request.AccountId, request.DeviceId, request.CommandId, "command.enqueue", "accepted", cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new DispatchEnqueueResult(DispatchEnqueueStatus.Accepted, Accepted(request.CommandId, seq));
            }
        }
    }

    private async Task<DispatchEnqueueResult?> ReplayCoreAsync(EnqueueDispatchCommand request, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                return await ReadIdempotentAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<DispatchCommandRecord?> GetCommandCoreAsync(string accountId, string commandId, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                var loaded = await ReadCommandAsync(connection, transaction, accountId, commandId, cancellationToken).ConfigureAwait(false);
                if (loaded is null)
                {
                    return null;
                }

                if (ShouldPurge(loaded.State, loaded.Meta))
                {
                    loaded = await PurgeBodyAsync(connection, transaction, accountId, commandId, loaded, cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return loaded.ToRecord();
            }
        }
    }

    private async Task<IReadOnlyList<DispatchCommandRecord>> ListQueuedCoreAsync(string accountId, string deviceId, long afterSeq, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            if (await CountOwnedAsync(connection, accountId, deviceId, cancellationToken).ConfigureAwait(false) == 0)
            {
                return [];
            }

            using var command = PostgresSql.Create(connection, null);
            command.CommandText = CommandSelect + " WHERE account_id = @account_id AND device_id = @device_id AND state = @state AND seq > @after AND expires_at > @now ORDER BY seq";
            command.Parameters.AddWithValue("account_id", accountId);
            command.Parameters.AddWithValue("device_id", deviceId);
            command.Parameters.AddWithValue("state", Queued);
            command.Parameters.AddWithValue("after", afterSeq);
            command.Parameters.AddWithValue("now", _timeProvider.GetUtcNow());
            var list = new List<DispatchCommandRecord>();
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    list.Add(ReadCommand(reader, accountId).ToRecord());
                }
            }

            return list;
        }
    }

    private async Task<IReadOnlyList<DispatchCommandRecord>> ListCommandsCoreAsync(string accountId, string? deviceId, int limit, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            using var command = PostgresSql.Create(connection, null);
            command.CommandText = CommandSelect + " WHERE account_id = @account_id AND (@device_id::text IS NULL OR device_id = @device_id) ORDER BY created_at DESC, id DESC LIMIT @limit";
            command.Parameters.AddWithValue("account_id", accountId);
            command.Parameters.AddWithValue("device_id", (object?)deviceId ?? DBNull.Value);
            command.Parameters.AddWithValue("limit", limit);
            var list = new List<DispatchCommandRecord>();
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    list.Add(ReadCommand(reader, accountId).ToRecord());
                }
            }

            return list;
        }
    }

    private async Task<DispatchTransitionResult> TransitionCoreAsync(string accountId, string commandId, DispatchCommandState expected, DispatchCommandState desired, DispatchFailureReason? reason, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                var loaded = await ReadCommandAsync(connection, transaction, accountId, commandId, cancellationToken).ConfigureAwait(false);
                if (loaded is null || !DispatchCommandStateWire.TryParse(loaded.State, out var current))
                {
                    return new DispatchTransitionResult(DispatchTransitionStatus.NotFound, null);
                }

                if (current != expected || !DispatchTransitions.Allows(expected, desired))
                {
                    await AuditAsync(connection, transaction, accountId, loaded.DeviceId, commandId, "command.transition", "rejected", cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return new DispatchTransitionResult(DispatchTransitionStatus.Rejected, current);
                }

                await ApplyTransitionAsync(connection, transaction, accountId, commandId, desired, reason, loaded, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new DispatchTransitionResult(DispatchTransitionStatus.Applied, desired);
            }
        }
    }

    private async Task<DispatchFeedEventRecord> AppendFeedCoreAsync(string accountId, string kind, string? payload, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await LockAccountAsync(connection, transaction, accountId, now, cancellationToken).ConfigureAwait(false);
                await PruneFeedAsync(connection, transaction, accountId, now, cancellationToken).ConfigureAwait(false);
                var id = await IncrementFeedAsync(connection, transaction, accountId, cancellationToken).ConfigureAwait(false);
                await InsertFeedAsync(connection, transaction, accountId, id, now, kind, payload, cancellationToken).ConfigureAwait(false);
                await AuditAsync(connection, transaction, accountId, null, null, "feed.append", "applied", cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new DispatchFeedEventRecord(id, accountId, kind, payload, now);
            }
        }
    }

    private async Task<DispatchFeedPage> ReadFeedCoreAsync(string accountId, long afterId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                if (await CountAccountAsync(connection, transaction, accountId, cancellationToken).ConfigureAwait(false) == 0)
                {
                    return new DispatchFeedPage(false, []);
                }

                await LockFeedAsync(connection, transaction, accountId, cancellationToken).ConfigureAwait(false);
                await PruneFeedAsync(connection, transaction, accountId, now, cancellationToken).ConfigureAwait(false);
                var seq = await ReadFeedSeqAsync(connection, transaction, accountId, cancellationToken).ConfigureAwait(false);
                var oldest = await ReadOldestFeedAsync(connection, transaction, accountId, cancellationToken).ConfigureAwait(false);
                var floor = oldest ?? seq + 1;
                var events = await ReadFeedEventsAsync(connection, transaction, accountId, afterId, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new DispatchFeedPage(afterId + 1 < floor, events);
            }
        }
    }


    private async Task<bool> FinishUpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, NpgsqlCommand command, string accountId, string deviceId, string op, CancellationToken cancellationToken)
    {
        var updated = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
        if (updated)
        {
            await AuditAsync(connection, transaction, accountId, deviceId, null, op, "applied", cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return updated;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken) =>
        await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    private bool ShouldPurge(string state, CommandMeta meta)
    {
        if (meta.TerminalAt is not { Length: > 0 } terminalAt || state is not ("completed" or "failed" or "cancelled" or "expired"))
        {
            return false;
        }

        var terminal = ParseTime(terminalAt);
        return _timeProvider.GetUtcNow() - terminal > _limits.BodyRetention;
    }

    private static async Task InsertDeviceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CreateDispatchDevice request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var insert = PostgresSql.Create(connection, transaction);
        insert.CommandText =
            "INSERT INTO dispatch_devices (id, account_id, name, platform, public_key, created_at, credential_jti, credential_expires_at, capabilities, paired_session_id) " +
            "VALUES (@id, @account_id, @name, @platform, @public_key, @created_at, @jti, @expires, CAST(@capabilities AS jsonb), @session)";
        insert.Parameters.AddWithValue("id", request.DeviceId);
        insert.Parameters.AddWithValue("account_id", request.AccountId);
        insert.Parameters.AddWithValue("name", request.Name);
        insert.Parameters.AddWithValue("platform", Platform);
        insert.Parameters.AddWithValue("public_key", request.PublicKey ?? string.Empty);
        insert.Parameters.AddWithValue("created_at", now);
        insert.Parameters.AddWithValue("jti", (object?)request.CredentialJti ?? DBNull.Value);
        insert.Parameters.AddWithValue("expires", request.CredentialExpiresAt.HasValue ? request.CredentialExpiresAt.Value : DBNull.Value);
        insert.Parameters.AddWithValue("capabilities", WriteCapabilities(request.Capabilities));
        insert.Parameters.AddWithValue("session", (object?)request.PairedSessionId ?? DBNull.Value);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> LockLiveDeviceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, EnqueueDispatchCommand request, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "SELECT CASE WHEN revoked_at IS NULL THEN 1 ELSE 0 END FROM dispatch_devices WHERE id = @id AND account_id = @account_id FOR UPDATE";
        command.Parameters.AddWithValue("id", request.DeviceId);
        command.Parameters.AddWithValue("account_id", request.AccountId);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is long live ? live == 1 : value is int liveInt && liveInt == 1;
    }

    private static async Task<DispatchEnqueueResult?> ReadIdempotentAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, EnqueueDispatchCommand request, CancellationToken cancellationToken)
    {
        if (request.IdempotencyKey is not { Length: > 0 })
        {
            return null;
        }

        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "SELECT id, seq, coalesce(result::text, '') FROM dispatch_commands WHERE account_id = @account_id AND device_id = @device_id AND idempotency_key = @key";
        command.Parameters.AddWithValue("account_id", request.AccountId);
        command.Parameters.AddWithValue("device_id", request.DeviceId);
        command.Parameters.AddWithValue("key", request.IdempotencyKey);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var meta = CommandMeta.Read(reader.GetString(2));
            if (!string.Equals(meta.Fingerprint, request.Fingerprint, StringComparison.Ordinal))
            {
                return new DispatchEnqueueResult(DispatchEnqueueStatus.IdempotencyKeyReused, null);
            }

            return new DispatchEnqueueResult(DispatchEnqueueStatus.Replayed, Accepted(reader.GetString(0), reader.GetInt64(1)));
        }
    }

    private static async Task<long> CountQueuedAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, EnqueueDispatchCommand request, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "SELECT count(*) FROM dispatch_commands WHERE account_id = @account_id AND device_id = @device_id AND state = @state";
        command.Parameters.AddWithValue("account_id", request.AccountId);
        command.Parameters.AddWithValue("device_id", request.DeviceId);
        command.Parameters.AddWithValue("state", Queued);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
    }

    private static async Task<long> AllocateSeqAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, EnqueueDispatchCommand request, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "UPDATE dispatch_devices SET command_seq = command_seq + 1 WHERE id = @id AND account_id = @account_id RETURNING command_seq";
        command.Parameters.AddWithValue("id", request.DeviceId);
        command.Parameters.AddWithValue("account_id", request.AccountId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Device row disappeared under its lock."));
    }

    private static async Task InsertCommandAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, EnqueueDispatchCommand request, long seq, CancellationToken cancellationToken)
    {
        var meta = new CommandMeta(request.Fingerprint, DispatchCapabilityWire.ToWire(request.Capability), request.IssuedAt.ToString("o", CultureInfo.InvariantCulture), string.Empty, string.Empty, string.Empty);
        using var insert = PostgresSql.Create(connection, transaction);
        insert.CommandText =
            "INSERT INTO dispatch_commands (id, device_id, account_id, seq, op, state, idempotency_key, expires_at, body, result, created_at, updated_at) " +
            "VALUES (@id, @device_id, @account_id, @seq, @op, @state, @key, @expires, CAST(@body AS jsonb), CAST(@result AS jsonb), @created, @updated)";
        insert.Parameters.AddWithValue("id", request.CommandId);
        insert.Parameters.AddWithValue("device_id", request.DeviceId);
        insert.Parameters.AddWithValue("account_id", request.AccountId);
        insert.Parameters.AddWithValue("seq", seq);
        insert.Parameters.AddWithValue("op", DispatchOpWire.ToWire(request.Op));
        insert.Parameters.AddWithValue("state", Queued);
        insert.Parameters.AddWithValue("key", request.IdempotencyKey ?? request.CommandId);
        insert.Parameters.AddWithValue("expires", request.ExpiresAt);
        insert.Parameters.AddWithValue("body", request.BodyJson);
        insert.Parameters.AddWithValue("result", meta.Write());
        insert.Parameters.AddWithValue("created", request.IssuedAt);
        insert.Parameters.AddWithValue("updated", request.IssuedAt);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<LoadedCommand?> ReadCommandAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, string commandId, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = CommandSelect + " WHERE id = @id AND account_id = @account_id FOR UPDATE";
        command.Parameters.AddWithValue("id", commandId);
        command.Parameters.AddWithValue("account_id", accountId);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadCommand(reader, accountId) : null;
        }
    }

    private async Task<LoadedCommand> PurgeBodyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, string commandId, LoadedCommand loaded, CancellationToken cancellationToken)
    {
        var meta = loaded.Meta with { Text = string.Empty };
        using var purge = PostgresSql.Create(connection, transaction);
        purge.CommandText = "UPDATE dispatch_commands SET body = NULL, body_purged_at = @purged, result = CAST(@result AS jsonb) WHERE id = @id AND account_id = @account_id";
        purge.Parameters.AddWithValue("purged", _timeProvider.GetUtcNow());
        purge.Parameters.AddWithValue("result", meta.Write());
        purge.Parameters.AddWithValue("id", commandId);
        purge.Parameters.AddWithValue("account_id", accountId);
        await purge.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await AuditAsync(connection, transaction, accountId, loaded.DeviceId, commandId, "command.purge", "applied", cancellationToken).ConfigureAwait(false);
        return loaded with { Body = null, Meta = meta };
    }

    private async Task ApplyTransitionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, string commandId, DispatchCommandState desired, DispatchFailureReason? reason, LoadedCommand loaded, CancellationToken cancellationToken)
    {
        var terminal = desired is DispatchCommandState.Completed or DispatchCommandState.Failed or DispatchCommandState.Cancelled or DispatchCommandState.Expired;
        var meta = loaded.Meta with
        {
            Reason = reason is { } failure ? DispatchFailureReasonWire.ToWire(failure) : loaded.Meta.Reason,
            TerminalAt = terminal ? _timeProvider.GetUtcNow().ToString("o", CultureInfo.InvariantCulture) : loaded.Meta.TerminalAt,
        };
        using var update = PostgresSql.Create(connection, transaction);
        update.CommandText = "UPDATE dispatch_commands SET state = @state, updated_at = @updated, result = CAST(@result AS jsonb) WHERE id = @id AND account_id = @account_id";
        update.Parameters.AddWithValue("state", DispatchCommandStateWire.ToWire(desired));
        update.Parameters.AddWithValue("updated", _timeProvider.GetUtcNow());
        update.Parameters.AddWithValue("result", meta.Write());
        update.Parameters.AddWithValue("id", commandId);
        update.Parameters.AddWithValue("account_id", accountId);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await AuditAsync(connection, transaction, accountId, loaded.DeviceId, commandId, "command.transition", "applied", cancellationToken).ConfigureAwait(false);
    }

    private async Task PruneFeedAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "SELECT id, occurred_at::text FROM dispatch_events WHERE account_id = @account_id";
        command.Parameters.AddWithValue("account_id", accountId);
        var expired = new List<long>();
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (now - ParseTime(reader.GetString(1)) > _limits.FeedRetention)
                {
                    expired.Add(reader.GetInt64(0));
                }
            }
        }

        if (expired.Count == 0)
        {
            return;
        }

        using var delete = PostgresSql.Create(connection, transaction);
        delete.CommandText = "DELETE FROM dispatch_events WHERE account_id = @account_id AND id = ANY(@ids)";
        delete.Parameters.AddWithValue("account_id", accountId);
        delete.Parameters.AddWithValue("ids", expired.ToArray());
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task LockAccountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        using (var insert = PostgresSql.Create(connection, transaction))
        {
            insert.CommandText = "INSERT INTO accounts (id, created_at, last_seen_at) VALUES (@id, @now, @now) ON CONFLICT (id) DO NOTHING";
            insert.Parameters.AddWithValue("id", accountId);
            insert.Parameters.AddWithValue("now", now);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "SELECT id FROM accounts WHERE id = @id FOR UPDATE";
        command.Parameters.AddWithValue("id", accountId);
        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> CountActiveDevicesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "SELECT count(*) FROM dispatch_devices WHERE account_id = @account_id AND revoked_at IS NULL";
        command.Parameters.AddWithValue("account_id", accountId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
    }

    private static async Task<bool> DeviceExistsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string deviceId, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "SELECT count(*) FROM dispatch_devices WHERE id = @id";
        command.Parameters.AddWithValue("id", deviceId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L) > 0;
    }

    private static async Task<long> CountOwnedAsync(NpgsqlConnection connection, string accountId, string deviceId, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, null);
        command.CommandText = "SELECT count(*) FROM dispatch_devices WHERE id = @id AND account_id = @account_id AND revoked_at IS NULL";
        command.Parameters.AddWithValue("id", deviceId);
        command.Parameters.AddWithValue("account_id", accountId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
    }

    private static async Task<long> CountAccountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "SELECT count(*) FROM accounts WHERE id = @id";
        command.Parameters.AddWithValue("id", accountId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
    }

    private static async Task LockFeedAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "SELECT feed_seq FROM accounts WHERE id = @id FOR UPDATE";
        command.Parameters.AddWithValue("id", accountId);
        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> IncrementFeedAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "UPDATE accounts SET feed_seq = feed_seq + 1 WHERE id = @id RETURNING feed_seq";
        command.Parameters.AddWithValue("id", accountId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
    }

    private static async Task InsertFeedAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, long id, DateTimeOffset now, string kind, string? payload, CancellationToken cancellationToken)
    {
        using var insert = PostgresSql.Create(connection, transaction);
        insert.CommandText = "INSERT INTO dispatch_events (account_id, id, occurred_at, kind, payload) VALUES (@account_id, @id, @at, @kind, CAST(@payload AS jsonb))";
        insert.Parameters.AddWithValue("account_id", accountId);
        insert.Parameters.AddWithValue("id", id);
        insert.Parameters.AddWithValue("at", now);
        insert.Parameters.AddWithValue("kind", kind);
        insert.Parameters.AddWithValue("payload", (object?)payload ?? DBNull.Value);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> ReadFeedSeqAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "SELECT feed_seq FROM accounts WHERE id = @id";
        command.Parameters.AddWithValue("id", accountId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
    }

    private static async Task<long?> ReadOldestFeedAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "SELECT coalesce(min(id), -1) FROM dispatch_events WHERE account_id = @account_id";
        command.Parameters.AddWithValue("account_id", accountId);
        var value = (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? -1L);
        return value < 0 ? null : value;
    }

    private static async Task<List<DispatchFeedEventRecord>> ReadFeedEventsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, long afterId, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "SELECT id, kind, coalesce(payload::text, ''), occurred_at::text FROM dispatch_events WHERE account_id = @account_id AND id > @after ORDER BY id";
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("after", afterId);
        var events = new List<DispatchFeedEventRecord>();
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var payload = reader.GetString(2);
                events.Add(new DispatchFeedEventRecord(reader.GetInt64(0), accountId, reader.GetString(1), payload.Length == 0 ? null : payload, ParseTime(reader.GetString(3))));
            }
        }

        return events;
    }

    private async Task AuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, string? deviceId, string? commandId, string op, string outcome, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = "INSERT INTO dispatch_audit (account_id_hash, occurred_at, device_id, command_id, op, outcome) VALUES (@hash, @at, @device_id, @command_id, @op, @outcome)";
        command.Parameters.AddWithValue("hash", _tenantHash(accountId));
        command.Parameters.AddWithValue("at", _timeProvider.GetUtcNow());
        command.Parameters.AddWithValue("device_id", (object?)deviceId ?? DBNull.Value);
        command.Parameters.AddWithValue("command_id", (object?)commandId ?? DBNull.Value);
        command.Parameters.AddWithValue("op", op);
        command.Parameters.AddWithValue("outcome", outcome);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DispatchCommandAcceptedResponse Accepted(string commandId, long seq) => new()
    {
        CommandId = commandId,
        Seq = seq,
        State = Queued,
    };

    private static DispatchDeviceRecord ReadDevice(NpgsqlDataReader reader) => new(
        reader.GetString(1),
        reader.GetString(0),
        reader.GetString(2),
        EmptyToNull(reader.GetString(3)),
        EmptyToNull(reader.GetString(4)),
        ParseOptional(reader.GetString(5)),
        ReadCapabilities(reader.GetString(6)),
        EmptyToNull(reader.GetString(7)),
        ParseOptional(reader.GetString(8)),
        ParseOptional(reader.GetString(9)),
        EmptyToNull(reader.GetString(10)));

    private static DispatchDeviceRecord ToDevice(CreateDispatchDevice request) => new(
        request.AccountId,
        request.DeviceId,
        request.Name,
        request.PublicKey,
        request.CredentialJti,
        request.CredentialExpiresAt,
        request.Capabilities,
        request.PairedSessionId,
        null,
        null,
        null);

    private static LoadedCommand ReadCommand(NpgsqlDataReader reader, string accountId)
    {
        var bodyMissing = reader.GetInt32(6) == 1;
        return new LoadedCommand(
            accountId,
            reader.GetString(0),
            reader.GetString(10),
            reader.GetInt64(1),
            reader.GetString(3),
            reader.GetString(2),
            reader.GetString(4),
            bodyMissing ? null : reader.GetString(7),
            ParseTime(reader.GetString(9)),
            ParseTime(reader.GetString(5)),
            CommandMeta.Read(reader.GetString(8)));
    }

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    private static DateTimeOffset? ParseOptional(string value) => value.Length == 0 ? null : ParseTime(value);

    private static DateTimeOffset ParseTime(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string WriteCapabilities(IReadOnlyList<DispatchCapabilitySnapshot> capabilities)
    {
        var builder = new StringBuilder();
        builder.Append('[');
        for (var i = 0; i < capabilities.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            var item = capabilities[i];
            builder.Append("{\"id\":");
            builder.Append(JsonQuote(DispatchCapabilityWire.ToWire(item.Id)));
            builder.Append(",\"version\":");
            builder.Append(item.Version.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"enabled\":");
            builder.Append(item.Enabled ? "true" : "false");
            builder.Append('}');
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static List<DispatchCapabilitySnapshot> ReadCapabilities(string json)
    {
        using var document = JsonDocument.Parse(json);
        var list = new List<DispatchCapabilitySnapshot>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!DispatchCapabilityWire.TryParse(item.GetProperty("id").GetString(), out var id))
            {
                throw new InvalidOperationException("Stored capability snapshot has an unknown id.");
            }

            list.Add(new DispatchCapabilitySnapshot(id, item.GetProperty("version").GetInt32(), item.GetProperty("enabled").GetBoolean()));
        }

        return list;
    }

    private static string JsonQuote(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                default:
                    if (char.IsControl(ch))
                    {
                        builder.Append("\\u");
                        builder.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(ch);
                    }

                    break;
            }
        }

        builder.Append('"');
        return builder.ToString();
    }

    private sealed record LoadedCommand(
        string AccountId,
        string DeviceId,
        string CommandId,
        long Seq,
        string State,
        string Op,
        string Key,
        string? Body,
        DateTimeOffset IssuedAt,
        DateTimeOffset ExpiresAt,
        CommandMeta Meta)
    {
        public DispatchCommandRecord ToRecord()
        {
            if (!DispatchCommandStateWire.TryParse(State, out var state) || !DispatchOpWire.TryParse(Op, out var op) || !DispatchCapabilityWire.TryParse(Meta.Capability, out var capability))
            {
                throw new InvalidOperationException("Stored dispatch command has an unknown closed value.");
            }

            DispatchFailureReason? reason = null;
            if (Meta.Reason.Length > 0)
            {
                if (!DispatchFailureReasonWire.TryParse(Meta.Reason, out var parsed))
                {
                    throw new InvalidOperationException("Stored dispatch command has an unknown failure reason.");
                }

                reason = parsed;
            }

            return new DispatchCommandRecord(AccountId, DeviceId, CommandId, Seq, state, capability, op, Meta.Fingerprint, Key, Body, IssuedAt, ExpiresAt, reason);
        }
    }

    private sealed record CommandMeta(string Fingerprint, string Capability, string IssuedAt, string Reason, string Text, string TerminalAt)
    {
        public string Write()
        {
            var builder = new StringBuilder();
            builder.Append("{\"fp\":");
            builder.Append(JsonQuote(Fingerprint));
            builder.Append(",\"cap\":");
            builder.Append(JsonQuote(Capability));
            builder.Append(",\"issued\":");
            builder.Append(JsonQuote(IssuedAt));
            builder.Append(",\"reason\":");
            builder.Append(Reason.Length == 0 ? "null" : JsonQuote(Reason));
            builder.Append(",\"text\":");
            builder.Append(Text.Length == 0 ? "null" : JsonQuote(Text));
            builder.Append(",\"terminalAt\":");
            builder.Append(TerminalAt.Length == 0 ? "null" : JsonQuote(TerminalAt));
            builder.Append('}');
            return builder.ToString();
        }

        public static CommandMeta Read(string json)
        {
            if (json.Length == 0)
            {
                throw new InvalidOperationException("Stored dispatch command is missing its result document.");
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new CommandMeta(
                root.GetProperty("fp").GetString() ?? string.Empty,
                root.GetProperty("cap").GetString() ?? string.Empty,
                root.GetProperty("issued").GetString() ?? string.Empty,
                root.GetProperty("reason").ValueKind == JsonValueKind.Null ? string.Empty : root.GetProperty("reason").GetString() ?? string.Empty,
                root.GetProperty("text").ValueKind == JsonValueKind.Null ? string.Empty : root.GetProperty("text").GetString() ?? string.Empty,
                root.GetProperty("terminalAt").ValueKind == JsonValueKind.Null ? string.Empty : root.GetProperty("terminalAt").GetString() ?? string.Empty);
        }
    }
}
