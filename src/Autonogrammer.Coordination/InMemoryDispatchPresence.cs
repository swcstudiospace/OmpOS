namespace Autonogrammer.Coordination;

/// <summary>
/// Process-local <see cref="IDispatchPresence"/>. Keys are <c>(accountId, deviceId)</c>. Lifetimes are measured on the
/// injected clock. This twin has no remote store, so a read is online or offline, never unknown.
/// </summary>
public sealed class InMemoryDispatchPresence : IDispatchPresence
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<(string AccountId, string DeviceId), Entry> _entries = new();
    private readonly Lock _gate = new();

    /// <summary>Creates an empty presence map that reads time from <paramref name="timeProvider"/>.</summary>
    /// <param name="timeProvider">The clock that decides when a holder expires.</param>
    public InMemoryDispatchPresence(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task SetAsync(string accountId, string deviceId, string holder, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        Validate(accountId, deviceId, holder, timeToLive);
        cancellationToken.ThrowIfCancellationRequested();

        var expiresAt = _timeProvider.GetUtcNow() + timeToLive;
        lock (_gate)
        {
            _entries[(accountId, deviceId)] = new Entry(holder, expiresAt);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> RefreshAsync(string accountId, string deviceId, string holder, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        Validate(accountId, deviceId, holder, timeToLive);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_entries.TryGetValue((accountId, deviceId), out var entry) || entry.ExpiresAt <= _timeProvider.GetUtcNow() || !string.Equals(entry.Holder, holder, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            _entries[(accountId, deviceId)] = entry with { ExpiresAt = _timeProvider.GetUtcNow() + timeToLive };
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task RemoveAsync(string accountId, string deviceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _entries.Remove((accountId, deviceId));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<DispatchPresence> GetAsync(string accountId, string deviceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_entries.TryGetValue((accountId, deviceId), out var entry) || entry.ExpiresAt <= _timeProvider.GetUtcNow())
            {
                return Task.FromResult(DispatchPresence.Offline);
            }

            return Task.FromResult(DispatchPresence.Online(entry.Holder));
        }
    }

    private static void Validate(string accountId, string deviceId, string holder, TimeSpan timeToLive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(holder);
        if (timeToLive <= TimeSpan.Zero || timeToLive > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), timeToLive, "Every presence key needs a time to live of more than zero and at most 24 hours.");
        }
    }

    private sealed record Entry(string Holder, DateTimeOffset ExpiresAt);
}
