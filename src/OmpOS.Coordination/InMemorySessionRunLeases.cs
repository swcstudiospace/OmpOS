namespace OmpOS.Coordination;

/// <summary>
/// Process-local <see cref="ISessionRunLeases"/>. A lease is live while its expiry lies after the current time on the
/// injected clock, so a test can expire leases by advancing that clock. All state changes happen under one lock.
/// </summary>
public sealed class InMemorySessionRunLeases : ISessionRunLeases
{
    /// <summary>The shortest lease lifetime accepted. Matches the resolution of the Dragonfly implementation.</summary>
    internal static readonly TimeSpan MinimumTimeToLive = TimeSpan.FromMilliseconds(1);

    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, LeaseEntry> _leases = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Creates an empty lease store that reads time from <paramref name="timeProvider"/>.</summary>
    /// <param name="timeProvider">The clock that decides when leases expire.</param>
    public InMemorySessionRunLeases(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>Attempts to take the exclusive run lease for a session.</summary>
    /// <param name="sessionId">The session to lease.</param>
    /// <param name="ownerId">The identity of the caller that will hold the lease.</param>
    /// <param name="timeToLive">How long the lease lasts unless renewed.</param>
    /// <param name="cancellationToken">Not observed: this operation completes synchronously.</param>
    /// <returns>The acquired lease, or <see langword="null"/> when another owner holds an unexpired lease.</returns>
    public Task<ISessionRunLease?> TryAcquireAsync(string sessionId, string ownerId, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeToLive, MinimumTimeToLive);

        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            if (IsLive(sessionId, now))
            {
                return Task.FromResult<ISessionRunLease?>(null);
            }

            _leases[sessionId] = new LeaseEntry(ownerId, now + timeToLive);
            return Task.FromResult<ISessionRunLease?>(new InMemorySessionRunLease(this, sessionId, ownerId));
        }
    }

    /// <summary>Reports whether an unexpired lease exists for a session, whoever holds it.</summary>
    /// <param name="sessionId">The session to inspect.</param>
    /// <param name="cancellationToken">Not observed: this operation completes synchronously.</param>
    /// <returns><see langword="true"/> when a live lease exists.</returns>
    public Task<bool> IsHeldAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        lock (_gate)
        {
            return Task.FromResult(IsLive(sessionId, _timeProvider.GetUtcNow()));
        }
    }

    /// <summary>Extends a lease that the given owner still holds. Returns false when it expired or was taken.</summary>
    internal bool TryRenew(string sessionId, string ownerId, TimeSpan timeToLive)
    {
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            if (!_leases.TryGetValue(sessionId, out var entry)
                || entry.ExpiresAt <= now
                || !string.Equals(entry.OwnerId, ownerId, StringComparison.Ordinal))
            {
                return false;
            }

            _leases[sessionId] = new LeaseEntry(ownerId, now + timeToLive);
            return true;
        }
    }

    /// <summary>Removes a lease only while the given owner still holds it. Any other caller is a no-op.</summary>
    internal void Release(string sessionId, string ownerId)
    {
        lock (_gate)
        {
            if (_leases.TryGetValue(sessionId, out var entry) && string.Equals(entry.OwnerId, ownerId, StringComparison.Ordinal))
            {
                _leases.Remove(sessionId);
            }
        }
    }

    /// <summary>Must be called while holding the lock.</summary>
    private bool IsLive(string sessionId, DateTimeOffset now)
    {
        return _leases.TryGetValue(sessionId, out var entry) && entry.ExpiresAt > now;
    }

    private sealed record LeaseEntry(string OwnerId, DateTimeOffset ExpiresAt);
}
