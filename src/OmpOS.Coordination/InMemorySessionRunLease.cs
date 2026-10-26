namespace OmpOS.Coordination;

/// <summary>A lease handed out by <see cref="InMemorySessionRunLeases"/>.</summary>
internal sealed class InMemorySessionRunLease : ISessionRunLease
{
    private readonly InMemorySessionRunLeases _leases;

    /// <summary>Wraps a lease that <paramref name="leases"/> has just granted.</summary>
    /// <param name="leases">The store that granted the lease.</param>
    /// <param name="sessionId">The leased session.</param>
    /// <param name="ownerId">The owner that holds the lease.</param>
    public InMemorySessionRunLease(InMemorySessionRunLeases leases, string sessionId, string ownerId)
    {
        _leases = leases;
        SessionId = sessionId;
        OwnerId = ownerId;
    }

    /// <summary>Gets the session the lease covers.</summary>
    public string SessionId { get; }

    /// <summary>Gets the owner identity that acquired the lease.</summary>
    public string OwnerId { get; }

    /// <summary>Extends the lease. Returns false when it expired or another owner took it.</summary>
    /// <param name="timeToLive">The new lifetime, measured from now.</param>
    /// <param name="cancellationToken">Not observed: this operation completes synchronously.</param>
    /// <returns><see langword="true"/> when the lease was renewed.</returns>
    public Task<bool> RenewAsync(TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeToLive, InMemorySessionRunLeases.MinimumTimeToLive);
        return Task.FromResult(_leases.TryRenew(SessionId, OwnerId, timeToLive));
    }

    /// <summary>Releases the lease, but only while this owner still holds it.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        _leases.Release(SessionId, OwnerId);
        return ValueTask.CompletedTask;
    }
}
