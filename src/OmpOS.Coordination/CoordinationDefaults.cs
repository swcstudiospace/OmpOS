namespace OmpOS.Coordination;

/// <summary>Timing defaults shared by every coordination implementation.</summary>
public static class CoordinationDefaults
{
    /// <summary>Gets how long a run lease lasts without renewal.</summary>
    public static TimeSpan RunLeaseTimeToLive { get; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets how often the owner of a run lease renews it. This is well inside <see cref="RunLeaseTimeToLive"/>.</summary>
    public static TimeSpan RunLeaseRenewInterval { get; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets how long a pending idempotency claim lasts before another request may claim the key.</summary>
    public static TimeSpan IdempotencyPendingTimeToLive { get; } = TimeSpan.FromSeconds(60);

    /// <summary>Gets how long a completed idempotency response is kept for replay.</summary>
    public static TimeSpan IdempotencyCompletedTimeToLive { get; } = TimeSpan.FromHours(24);
}
