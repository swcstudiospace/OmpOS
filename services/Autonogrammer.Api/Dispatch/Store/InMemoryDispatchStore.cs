using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Api.Dispatch.Store;

/// <summary>
/// Process-local <see cref="IDispatchStore"/>. Sequence allocation, the queue cap and the device cap share one lock.
/// Feed identifiers are the in-memory twin of <c>accounts.feed_seq</c>.
/// </summary>
internal sealed class InMemoryDispatchStore : IDispatchStore
{
    private readonly Lock _gate = new();
    private readonly DispatchStoreLimits _limits;
    private readonly TimeProvider _time;
    private readonly Dictionary<(string Account, string Device), DeviceRow> _devices = new();
    private readonly Dictionary<(string Account, string Command), CommandRow> _commands = new();
    private readonly Dictionary<(string Account, string Device, string Key), string> _idempotency = new();
    private readonly Dictionary<string, FeedRow> _feeds = new(StringComparer.Ordinal);

    /// <summary>Creates an empty store.</summary>
    /// <param name="limits">Caps and retention.</param>
    /// <param name="time">Clock used for feed and body retention.</param>
    public InMemoryDispatchStore(DispatchStoreLimits limits, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(time);
        if (limits.MaxQueuedPerDevice < 1 || limits.DevicesPerAccount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limits));
        }

        _limits = limits;
        _time = time;
    }

    /// <inheritdoc />
    public ValueTask<DispatchDeviceCreateResult> CreateDeviceAsync(CreateDispatchDevice request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var key = (request.AccountId, request.DeviceId);
            if (_devices.ContainsKey(key))
            {
                return ValueTask.FromResult(new DispatchDeviceCreateResult(DispatchDeviceCreateStatus.DeviceIdTaken, null));
            }

            var active = 0;
            foreach (var row in _devices.Values)
            {
                if (string.Equals(row.AccountId, request.AccountId, StringComparison.Ordinal) && row.RevokedAt is null)
                {
                    active++;
                }
            }

            if (active >= _limits.DevicesPerAccount)
            {
                return ValueTask.FromResult(new DispatchDeviceCreateResult(DispatchDeviceCreateStatus.DeviceLimitReached, null));
            }

            var created = new DeviceRow
            {
                AccountId = request.AccountId,
                DeviceId = request.DeviceId,
                Name = request.Name,
                PublicKey = request.PublicKey,
                CredentialJti = request.CredentialJti,
                CredentialExpiresAt = request.CredentialExpiresAt,
                Capabilities = request.Capabilities,
                PairedSessionId = request.PairedSessionId,
            };
            _devices.Add(key, created);
            return ValueTask.FromResult(new DispatchDeviceCreateResult(DispatchDeviceCreateStatus.Created, created.ToRecord()));
        }
    }

    /// <inheritdoc />
    public ValueTask<DispatchDeviceRecord?> GetDeviceAsync(string accountId, string deviceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return ValueTask.FromResult(_devices.TryGetValue((accountId, deviceId), out var row) ? row.ToRecord() : null);
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<DispatchDeviceRecord>> ListDevicesAsync(string accountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var list = new List<DispatchDeviceRecord>();
            foreach (var row in _devices.Values)
            {
                if (string.Equals(row.AccountId, accountId, StringComparison.Ordinal))
                {
                    list.Add(row.ToRecord());
                }
            }

            return ValueTask.FromResult<IReadOnlyList<DispatchDeviceRecord>>(list);
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> RenameDeviceAsync(string accountId, string deviceId, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(name);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_devices.TryGetValue((accountId, deviceId), out var row))
            {
                return ValueTask.FromResult(false);
            }

            row.Name = name;
            return ValueTask.FromResult(true);
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> RevokeDeviceAsync(string accountId, string deviceId, string? reason, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_devices.TryGetValue((accountId, deviceId), out var row))
            {
                return ValueTask.FromResult(false);
            }

            row.RevokedAt ??= revokedAt;
            row.RevokeReason = reason;
            row.CredentialJti = null;
            foreach (var command in _commands.Values)
            {
                if (!string.Equals(command.AccountId, accountId, StringComparison.Ordinal)
                    || !string.Equals(command.DeviceId, deviceId, StringComparison.Ordinal)
                    || RevokeTarget(command.State) is not { } target)
                {
                    continue;
                }

                command.State = target;
                command.Reason = DispatchFailureReason.Revoked;
                command.TerminalAt = revokedAt;
            }

            return ValueTask.FromResult(true);
        }
    }

    /// <summary>The state a device revoke moves a command to, or null when the command is already finished (D-30).</summary>
    private static DispatchCommandState? RevokeTarget(DispatchCommandState state) => state switch
    {
        DispatchCommandState.Queued or DispatchCommandState.Delivered => DispatchCommandState.Cancelled,
        DispatchCommandState.Received or DispatchCommandState.AwaitingApproval or DispatchCommandState.Accepted or DispatchCommandState.Running => DispatchCommandState.Failed,
        _ => null,
    };

    /// <inheritdoc />
    public ValueTask<bool> UpdateCredentialJtiAsync(string accountId, string deviceId, string credentialJti, DateTimeOffset credentialExpiresAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(credentialJti);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_devices.TryGetValue((accountId, deviceId), out var row))
            {
                return ValueTask.FromResult(false);
            }

            row.CredentialJti = credentialJti;
            row.CredentialExpiresAt = credentialExpiresAt;
            return ValueTask.FromResult(true);
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> UpdateCapabilitySnapshotAsync(string accountId, string deviceId, IReadOnlyList<DispatchCapabilitySnapshot> capabilities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(capabilities);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_devices.TryGetValue((accountId, deviceId), out var row))
            {
                return ValueTask.FromResult(false);
            }

            row.Capabilities = capabilities;
            return ValueTask.FromResult(true);
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> TouchLastSeenAsync(string accountId, string deviceId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_devices.TryGetValue((accountId, deviceId), out var row))
            {
                return ValueTask.FromResult(false);
            }

            row.LastSeenAt = lastSeenAt;
            return ValueTask.FromResult(true);
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> UpdatePairedSessionAsync(string accountId, string deviceId, string pairedSessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(pairedSessionId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_devices.TryGetValue((accountId, deviceId), out var row))
            {
                return ValueTask.FromResult(false);
            }

            row.PairedSessionId = pairedSessionId;
            return ValueTask.FromResult(true);
        }
    }

    /// <inheritdoc />
    public ValueTask<DispatchEnqueueResult> EnqueueAsync(EnqueueDispatchCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_devices.TryGetValue((request.AccountId, request.DeviceId), out var device) || device.RevokedAt is not null)
            {
                return ValueTask.FromResult(new DispatchEnqueueResult(DispatchEnqueueStatus.NotFound, null));
            }

            if (TryReplay(request, out var replayed))
            {
                return ValueTask.FromResult(replayed);
            }

            if (CountQueued(request.DeviceId) >= _limits.MaxQueuedPerDevice)
            {
                return ValueTask.FromResult(new DispatchEnqueueResult(DispatchEnqueueStatus.QueueFull, null));
            }

            return ValueTask.FromResult(Insert(device, request));
        }
    }

    /// <inheritdoc />
    public ValueTask<DispatchCommandRecord?> GetCommandAsync(string accountId, string commandId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(commandId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_commands.TryGetValue((accountId, commandId), out var row))
            {
                return ValueTask.FromResult<DispatchCommandRecord?>(null);
            }

            PurgeBody(row);
            return ValueTask.FromResult<DispatchCommandRecord?>(row.ToRecord());
        }
    }

    /// <inheritdoc />
    public ValueTask<DispatchEnqueueResult?> ReplayAsync(EnqueueDispatchCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return ValueTask.FromResult(TryReplay(request, out var result) ? result : (DispatchEnqueueResult?)null);
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<DispatchCommandRecord>> ListQueuedAfterAsync(string accountId, string deviceId, long afterSeq, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_devices.TryGetValue((accountId, deviceId), out var device) || device.RevokedAt is not null)
            {
                return ValueTask.FromResult<IReadOnlyList<DispatchCommandRecord>>([]);
            }

            var list = new List<DispatchCommandRecord>();
            foreach (var row in _commands.Values)
            {
                if (string.Equals(row.AccountId, accountId, StringComparison.Ordinal)
                    && string.Equals(row.DeviceId, deviceId, StringComparison.Ordinal)
                    && row.State is (DispatchCommandState.Queued or DispatchCommandState.Delivered)
                    && row.Seq > afterSeq)
                {
                    list.Add(row.ToRecord());
                }
            }

            list.Sort(static (left, right) => left.Seq.CompareTo(right.Seq));
            return ValueTask.FromResult<IReadOnlyList<DispatchCommandRecord>>(list);
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<DispatchCommandRecord>> ListCommandsAsync(string accountId, string? deviceId, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 100);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var list = new List<DispatchCommandRecord>();
            foreach (var row in _commands.Values)
            {
                if (string.Equals(row.AccountId, accountId, StringComparison.Ordinal)
                    && (deviceId is null || string.Equals(row.DeviceId, deviceId, StringComparison.Ordinal)))
                {
                    PurgeBody(row);
                    list.Add(row.ToRecord());
                }
            }

            list.Sort(static (left, right) => right.IssuedAt != left.IssuedAt
                ? right.IssuedAt.CompareTo(left.IssuedAt)
                : string.CompareOrdinal(right.CommandId, left.CommandId));
            return ValueTask.FromResult<IReadOnlyList<DispatchCommandRecord>>(list.Take(limit).ToList());
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<DispatchCommandRecord>> ListInFlightAfterAsync(string accountId, string deviceId, long afterSeq, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 100);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            IReadOnlyList<DispatchCommandRecord> rows =
            [
                .. _commands.Values
                    .Where(row => string.Equals(row.AccountId, accountId, StringComparison.Ordinal)
                        && string.Equals(row.DeviceId, deviceId, StringComparison.Ordinal)
                        && row.Seq > afterSeq
                        && row.State is (DispatchCommandState.Received or DispatchCommandState.AwaitingApproval or DispatchCommandState.Accepted or DispatchCommandState.Running))
                    .OrderBy(static row => row.Seq)
                    .Take(limit)
                    .Select(static row => row.ToRecord()),
            ];
            return ValueTask.FromResult(rows);
        }
    }

    /// <inheritdoc />
    public ValueTask<DispatchTransitionResult> TryTransitionAsync(
        string accountId,
        string commandId,
        DispatchCommandState expected,
        DispatchCommandState desired,
        DispatchFailureReason? reason,
        DispatchFeedDraft? feed,
        CancellationToken cancellationToken,
        string? resultText = null)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(commandId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_commands.TryGetValue((accountId, commandId), out var row))
            {
                return ValueTask.FromResult(new DispatchTransitionResult(DispatchTransitionStatus.NotFound, null));
            }

            if (row.State != expected || !DispatchTransitions.Allows(expected, desired))
            {
                return ValueTask.FromResult(new DispatchTransitionResult(DispatchTransitionStatus.Rejected, row.State));
            }

            var now = _time.GetUtcNow();
            row.State = desired;
            if (desired == DispatchCommandState.Received)
            {
                row.ReceivedAt = now;
            }

            row.StateEnteredAt = now;
            row.Reason = reason;
            row.ResultText = string.IsNullOrEmpty(resultText) ? null : resultText;
            if (desired is DispatchCommandState.Completed or DispatchCommandState.Failed or DispatchCommandState.Cancelled or DispatchCommandState.Expired)
            {
                row.TerminalAt = now;
            }

            if (feed is not null)
            {
                _ = AppendFeedLocked(accountId, feed.Kind, feed.Payload);
            }

            return ValueTask.FromResult(new DispatchTransitionResult(DispatchTransitionStatus.Applied, row.State));
        }
    }

    /// <inheritdoc />
    public ValueTask<DispatchTransitionResult> TryTimeoutAndEnqueueAsync(string accountId, string commandId, DispatchCommandState expected, EnqueueDispatchCommand cancellation, DispatchFeedDraft feed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(commandId);
        ArgumentNullException.ThrowIfNull(cancellation);
        ArgumentNullException.ThrowIfNull(feed);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_commands.TryGetValue((accountId, commandId), out var row))
            {
                return ValueTask.FromResult(new DispatchTransitionResult(DispatchTransitionStatus.NotFound, null));
            }

            if (row.State != expected || !DispatchTransitions.Allows(expected, DispatchCommandState.Failed)
                || expected is not (DispatchCommandState.Received or DispatchCommandState.Accepted or DispatchCommandState.Running)
                || row.Op == DispatchOp.CommandCancel || cancellation.Op != DispatchOp.CommandCancel
                || !string.Equals(cancellation.AccountId, accountId, StringComparison.Ordinal)
                || !string.Equals(cancellation.DeviceId, row.DeviceId, StringComparison.Ordinal)
                || !_devices.TryGetValue((accountId, row.DeviceId), out var device) || device.RevokedAt is not null
                || _commands.ContainsKey((accountId, cancellation.CommandId))
                || (cancellation.IdempotencyKey is { Length: > 0 } key && _idempotency.ContainsKey((accountId, row.DeviceId, key))))
            {
                return ValueTask.FromResult(new DispatchTransitionResult(DispatchTransitionStatus.Rejected, row.State));
            }

            _ = Insert(device, cancellation);
            var now = _time.GetUtcNow();
            row.State = DispatchCommandState.Failed;
            row.Reason = DispatchFailureReason.Timeout;
            row.StateEnteredAt = now;
            row.TerminalAt = now;
            _ = AppendFeedLocked(accountId, feed.Kind, feed.Payload);
            return ValueTask.FromResult(new DispatchTransitionResult(DispatchTransitionStatus.Applied, row.State));
        }
    }

    /// <inheritdoc />
    public ValueTask<DispatchTransitionResult> TryRequestCancelAsync(string accountId, string commandId, EnqueueDispatchCommand cancellation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(commandId);
        ArgumentNullException.ThrowIfNull(cancellation);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_commands.TryGetValue((accountId, commandId), out var row))
            {
                return ValueTask.FromResult(new DispatchTransitionResult(DispatchTransitionStatus.NotFound, null));
            }

            if (row.State is not (DispatchCommandState.Delivered or DispatchCommandState.Received or DispatchCommandState.AwaitingApproval or DispatchCommandState.Accepted or DispatchCommandState.Running)
                || row.Op == DispatchOp.CommandCancel || cancellation.Op != DispatchOp.CommandCancel
                || !string.Equals(cancellation.AccountId, accountId, StringComparison.Ordinal)
                || !string.Equals(cancellation.DeviceId, row.DeviceId, StringComparison.Ordinal)
                || !_devices.TryGetValue((accountId, row.DeviceId), out var device) || device.RevokedAt is not null
                || _commands.ContainsKey((accountId, cancellation.CommandId))
                || (cancellation.IdempotencyKey is { Length: > 0 } key && _idempotency.ContainsKey((accountId, row.DeviceId, key))))
            {
                return ValueTask.FromResult(new DispatchTransitionResult(DispatchTransitionStatus.Rejected, row.State));
            }

            _ = Insert(device, cancellation);
            return ValueTask.FromResult(new DispatchTransitionResult(DispatchTransitionStatus.Applied, row.State));
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> RecordSourcePrefixAsync(string accountId, string deviceId, string? sourcePrefix, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_devices.TryGetValue((accountId, deviceId), out var device))
            {
                return ValueTask.FromResult(false);
            }

            device.LastSourcePrefix = sourcePrefix;
            return ValueTask.FromResult(true);
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<DispatchSweepCandidate>> ListOverdueAsync(DispatchSweepCutoffs cutoffs, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cutoffs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            IReadOnlyList<DispatchSweepCandidate> overdue =
            [
                .. _commands.Values
                    .Where(row => IsOverdue(row, cutoffs))
                    .OrderBy(static row => row.StateEnteredAt)
                    .Take(limit)
                    .Select(static row => new DispatchSweepCandidate(row.AccountId, row.CommandId, row.State, row.Op, row.ExpiresAt, row.StateEnteredAt)),
            ];
            return ValueTask.FromResult(overdue);
        }
    }

    private static bool IsOverdue(CommandRow row, DispatchSweepCutoffs cutoffs) => row.State switch
    {
        DispatchCommandState.Queued or DispatchCommandState.Delivered => row.ExpiresAt <= cutoffs.Now,
        DispatchCommandState.AwaitingApproval => row.StateEnteredAt <= cutoffs.ApprovalBefore,
        DispatchCommandState.Received or DispatchCommandState.Accepted or DispatchCommandState.Running =>
            (row.ReceivedAt ?? row.StateEnteredAt) <= (DispatchRunClass.IsSessionScoped(row.Op) ? cutoffs.SessionBefore : cutoffs.TaskBefore),
        _ => false,
    };

    /// <inheritdoc />
    public ValueTask<DispatchFeedEventRecord> AppendFeedEventAsync(string accountId, string kind, string? payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(kind);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return ValueTask.FromResult(AppendFeedLocked(accountId, kind, payload));
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> TryAppendProgressAsync(string accountId, string deviceId, string commandId, string payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(commandId);
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_commands.TryGetValue((accountId, commandId), out var row)
                || !string.Equals(row.DeviceId, deviceId, StringComparison.Ordinal)
                || row.State is not (DispatchCommandState.Received or DispatchCommandState.AwaitingApproval or DispatchCommandState.Accepted or DispatchCommandState.Running)
                || row.ProgressCount >= 50)
            {
                return ValueTask.FromResult(false);
            }

            row.ProgressCount++;
            _ = AppendFeedLocked(accountId, "command.evt", payload);
            return ValueTask.FromResult(true);
        }
    }

    private DispatchFeedEventRecord AppendFeedLocked(string accountId, string kind, string? payload)
    {
        var feed = FeedFor(accountId);
        Prune(feed);
        feed.Seq++;
        var stored = new DispatchFeedEventRecord(feed.Seq, accountId, kind, payload, _time.GetUtcNow());
        feed.Events.Add(stored);
        return stored;
    }

    /// <inheritdoc />
    public ValueTask<DispatchFeedPage> ReadFeedAsync(string accountId, long afterId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var feed = FeedFor(accountId);
            Prune(feed);
            var oldest = feed.Events.Count == 0 ? feed.Seq + 1 : feed.Events[0].Id;
            var resync = afterId + 1 < oldest;
            var page = new List<DispatchFeedEventRecord>();
            foreach (var item in feed.Events)
            {
                if (item.Id > afterId)
                {
                    page.Add(item);
                }
            }

            return ValueTask.FromResult(new DispatchFeedPage(resync, page));
        }
    }

    private void PurgeBody(CommandRow row)
    {
        if (row.BodyJson is null || row.TerminalAt is not { } terminal)
        {
            return;
        }

        if (_time.GetUtcNow() - terminal > _limits.BodyRetention)
        {
            row.BodyJson = null;
            row.ResultText = null;
        }
    }

    private FeedRow FeedFor(string accountId)
    {
        if (!_feeds.TryGetValue(accountId, out var feed))
        {
            feed = new FeedRow();
            _feeds.Add(accountId, feed);
        }

        return feed;
    }

    private void Prune(FeedRow feed)
    {
        var now = _time.GetUtcNow();
        feed.Events.RemoveAll(item => now - item.At > _limits.FeedRetention);
    }

    private bool TryReplay(EnqueueDispatchCommand request, out DispatchEnqueueResult result)
    {
        result = new DispatchEnqueueResult(DispatchEnqueueStatus.NotFound, null);
        if (request.IdempotencyKey is not { Length: > 0 } key)
        {
            return false;
        }

        if (!_idempotency.TryGetValue((request.AccountId, request.DeviceId, key), out var existingId)
            || !_commands.TryGetValue((request.AccountId, existingId), out var existing))
        {
            return false;
        }

        if (!string.Equals(existing.Fingerprint, request.Fingerprint, StringComparison.Ordinal))
        {
            result = new DispatchEnqueueResult(DispatchEnqueueStatus.IdempotencyKeyReused, null);
            return true;
        }

        result = new DispatchEnqueueResult(DispatchEnqueueStatus.Replayed, existing.Accepted);
        return true;
    }

    private int CountQueued(string deviceId)
    {
        var queued = 0;
        foreach (var row in _commands.Values)
        {
            if (string.Equals(row.DeviceId, deviceId, StringComparison.Ordinal) && row.State == DispatchCommandState.Queued)
            {
                queued++;
            }
        }

        return queued;
    }

    private DispatchEnqueueResult Insert(DeviceRow device, EnqueueDispatchCommand request)
    {
        device.CommandSeq++;
        var accepted = new DispatchCommandAcceptedResponse
        {
            CommandId = request.CommandId,
            Seq = device.CommandSeq,
            State = DispatchCommandStateWire.ToWire(DispatchCommandState.Queued),
        };
        _commands.Add((request.AccountId, request.CommandId), new CommandRow
        {
            AccountId = request.AccountId,
            DeviceId = request.DeviceId,
            CommandId = request.CommandId,
            Seq = device.CommandSeq,
            State = DispatchCommandState.Queued,
            Capability = request.Capability,
            Op = request.Op,
            Fingerprint = request.Fingerprint,
            IdempotencyKey = request.IdempotencyKey,
            BodyJson = request.BodyJson,
            IssuedAt = request.IssuedAt,
            ExpiresAt = request.ExpiresAt,
            StateEnteredAt = request.IssuedAt,
            Accepted = accepted,
        });
        if (request.IdempotencyKey is { Length: > 0 } storedKey)
        {
            _idempotency[(request.AccountId, request.DeviceId, storedKey)] = request.CommandId;
        }

        return new DispatchEnqueueResult(DispatchEnqueueStatus.Accepted, accepted);
    }

    private sealed class DeviceRow
    {
        public required string AccountId { get; init; }

        public required string DeviceId { get; init; }

        public required string Name { get; set; }

        public string? PublicKey { get; init; }

        public string? CredentialJti { get; set; }

        public DateTimeOffset? CredentialExpiresAt { get; set; }

        public IReadOnlyList<DispatchCapabilitySnapshot> Capabilities { get; set; } = [];

        public string? PairedSessionId { get; set; }

        public DateTimeOffset? LastSeenAt { get; set; }

        public DateTimeOffset? RevokedAt { get; set; }

        public string? RevokeReason { get; set; }

        public string? LastSourcePrefix { get; set; }

        public long CommandSeq { get; set; }

        public DispatchDeviceRecord ToRecord() => new(
            AccountId,
            DeviceId,
            Name,
            PublicKey,
            CredentialJti,
            CredentialExpiresAt,
            Capabilities,
            PairedSessionId,
            LastSeenAt,
            RevokedAt,
            RevokeReason,
            LastSourcePrefix);
    }

    private sealed class CommandRow
    {
        public required string AccountId { get; init; }

        public required string DeviceId { get; init; }

        public required string CommandId { get; init; }

        public required long Seq { get; init; }

        public required DispatchCommandState State { get; set; }

        public required DispatchCapability Capability { get; init; }

        public required DispatchOp Op { get; init; }

        public required string Fingerprint { get; init; }

        public string? IdempotencyKey { get; init; }

        public string? BodyJson { get; set; }

        public required DateTimeOffset IssuedAt { get; init; }

        public required DateTimeOffset ExpiresAt { get; init; }

        public required DateTimeOffset StateEnteredAt { get; set; }

        public DateTimeOffset? ReceivedAt { get; set; }

        public DispatchFailureReason? Reason { get; set; }

        public string? ResultText { get; set; }

        public int ProgressCount { get; set; }

        public DateTimeOffset? TerminalAt { get; set; }

        public required DispatchCommandAcceptedResponse Accepted { get; init; }

        public DispatchCommandRecord ToRecord() => new(
            AccountId,
            DeviceId,
            CommandId,
            Seq,
            State,
            Capability,
            Op,
            Fingerprint,
            IdempotencyKey,
            BodyJson,
            IssuedAt,
            ExpiresAt,
            Reason,
            ResultText);
    }

    private sealed class FeedRow
    {
        public long Seq { get; set; }

        public List<DispatchFeedEventRecord> Events { get; } = [];
    }
}
