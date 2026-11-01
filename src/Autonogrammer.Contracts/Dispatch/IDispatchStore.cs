namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// Device rows, command rows and the account feed. Every operation is scoped to an account: a foreign account reads and changes nothing.
/// Sequence allocation, the queue cap and the device cap are atomic with the insert.
/// </summary>
public interface IDispatchStore
{
    /// <summary>Inserts a device, refusing the device past the account cap. A revoked device does not count.</summary>
    /// <param name="request">The device to insert.</param>
    /// <param name="cancellationToken">Cancels the insert.</param>
    /// <returns>Created, the cap refusal, or a taken identifier.</returns>
    ValueTask<DispatchDeviceCreateResult> CreateDeviceAsync(CreateDispatchDevice request, CancellationToken cancellationToken);

    /// <summary>Reads one device owned by the account.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="deviceId">The device.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row, or null when the account does not own it.</returns>
    ValueTask<DispatchDeviceRecord?> GetDeviceAsync(string accountId, string deviceId, CancellationToken cancellationToken);

    /// <summary>Lists devices owned by the account.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The account's devices. Never another account's.</returns>
    ValueTask<IReadOnlyList<DispatchDeviceRecord>> ListDevicesAsync(string accountId, CancellationToken cancellationToken);

    /// <summary>Renames a device the account owns.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="deviceId">The device.</param>
    /// <param name="name">The new name.</param>
    /// <param name="cancellationToken">Cancels the rename.</param>
    /// <returns><see langword="false"/> when the account does not own the device.</returns>
    ValueTask<bool> RenameDeviceAsync(string accountId, string deviceId, string name, CancellationToken cancellationToken);

    /// <summary>Revokes a device. A revoked device stops counting toward the cap.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="deviceId">The device.</param>
    /// <param name="reason">Optional reason.</param>
    /// <param name="revokedAt">Revocation time.</param>
    /// <param name="cancellationToken">Cancels the revoke.</param>
    /// <returns><see langword="false"/> when the account does not own the device.</returns>
    ValueTask<bool> RevokeDeviceAsync(string accountId, string deviceId, string? reason, DateTimeOffset revokedAt, CancellationToken cancellationToken);

    /// <summary>Replaces the credential identifier after a rotation.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="deviceId">The device.</param>
    /// <param name="credentialJti">The new identifier.</param>
    /// <param name="credentialExpiresAt">The new expiry.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns><see langword="false"/> when the account does not own the device.</returns>
    ValueTask<bool> UpdateCredentialJtiAsync(string accountId, string deviceId, string credentialJti, DateTimeOffset credentialExpiresAt, CancellationToken cancellationToken);

    /// <summary>Stores the newest capability snapshot from hello or pairing.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="deviceId">The device.</param>
    /// <param name="capabilities">The snapshot.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns><see langword="false"/> when the account does not own the device.</returns>
    ValueTask<bool> UpdateCapabilitySnapshotAsync(string accountId, string deviceId, IReadOnlyList<DispatchCapabilitySnapshot> capabilities, CancellationToken cancellationToken);

    /// <summary>Records a presence touch, at most as often as the caller asks.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="deviceId">The device.</param>
    /// <param name="lastSeenAt">The touch time.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns><see langword="false"/> when the account does not own the device.</returns>
    ValueTask<bool> TouchLastSeenAsync(string accountId, string deviceId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken);

    /// <summary>Records the sign-in session that paired the device.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="deviceId">The device.</param>
    /// <param name="pairedSessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns><see langword="false"/> when the account does not own the device.</returns>
    ValueTask<bool> UpdatePairedSessionAsync(string accountId, string deviceId, string pairedSessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Enqueues a command. Sequence numbers start at 1 and have no gaps. The same idempotency key and fingerprint replay the original 202 body.
    /// A different fingerprint is <see cref="DispatchEnqueueStatus.IdempotencyKeyReused"/>. The queued-command cap is checked in the same step as the insert.
    /// </summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the enqueue.</param>
    /// <returns>Accepted, replayed, reused, full, or not found.</returns>
    ValueTask<DispatchEnqueueResult> EnqueueAsync(EnqueueDispatchCommand request, CancellationToken cancellationToken);

    /// <summary>Reads one command the account owns.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="commandId">The command.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row, or null when the account does not own it.</returns>
    ValueTask<DispatchCommandRecord?> GetCommandAsync(string accountId, string commandId, CancellationToken cancellationToken);

    /// <summary>Lists queued commands with a sequence greater than <paramref name="afterSeq"/>, in order.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="deviceId">The device.</param>
    /// <param name="afterSeq">The last sequence already delivered.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Queued commands. Empty when the account does not own the device.</returns>
    ValueTask<IReadOnlyList<DispatchCommandRecord>> ListQueuedAfterAsync(string accountId, string deviceId, long afterSeq, CancellationToken cancellationToken);

    /// <summary>Compare-and-set against <see cref="DispatchTransitions"/>. An illegal edge leaves the row unchanged.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="commandId">The command.</param>
    /// <param name="expected">The state the row must currently have.</param>
    /// <param name="desired">The state to write.</param>
    /// <param name="reason">Failure reason when the next state carries one.</param>
    /// <param name="cancellationToken">Cancels the transition.</param>
    /// <returns>Applied, rejected, or not found.</returns>
    ValueTask<DispatchTransitionResult> TryTransitionAsync(string accountId, string commandId, DispatchCommandState expected, DispatchCommandState desired, DispatchFailureReason? reason, CancellationToken cancellationToken);

    /// <summary>Appends a feed event. Identifiers increase strictly per account.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="kind">Event kind.</param>
    /// <param name="payload">JSON payload.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The stored event.</returns>
    ValueTask<DispatchFeedEventRecord> AppendFeedEventAsync(string accountId, string kind, string? payload, CancellationToken cancellationToken);

    /// <summary>Reads feed events after a position. A position older than retained history is a resync page.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="afterId">The last identifier the caller has. Zero starts at the beginning of retained history.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page. Another account's events are never included.</returns>
    ValueTask<DispatchFeedPage> ReadFeedAsync(string accountId, long afterId, CancellationToken cancellationToken);
}
