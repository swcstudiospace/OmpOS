namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Result of enqueueing a command. The route maps these to 202, 409 and 404.</summary>
public enum DispatchEnqueueStatus
{
    /// <summary>A new command was queued.</summary>
    Accepted,

    /// <summary>The same idempotency key and fingerprint returned the original 202 body.</summary>
    Replayed,

    /// <summary>The same idempotency key was sent with a different fingerprint.</summary>
    IdempotencyKeyReused,

    /// <summary>The device already has the queued-command cap.</summary>
    QueueFull,

    /// <summary>The account does not own the device, or the device is revoked.</summary>
    NotFound,
}
