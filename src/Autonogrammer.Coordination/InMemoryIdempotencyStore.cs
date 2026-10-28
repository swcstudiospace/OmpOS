namespace Autonogrammer.Coordination;

/// <summary>
/// Process-local <see cref="IIdempotencyStore"/>. Pending claims expire after
/// <see cref="CoordinationDefaults.IdempotencyPendingTimeToLive"/> and completed responses after
/// <see cref="CoordinationDefaults.IdempotencyCompletedTimeToLive"/>, both measured on the injected clock.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<(string Scope, string Key), Entry> _entries = new();
    private readonly Lock _gate = new();

    /// <summary>Creates an empty store that reads time from <paramref name="timeProvider"/>.</summary>
    /// <param name="timeProvider">The clock that decides when records expire.</param>
    public InMemoryIdempotencyStore(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>Claims a key in a scope for a request with the given fingerprint.</summary>
    /// <param name="scope">The namespace of the key.</param>
    /// <param name="key">The client-supplied idempotency key.</param>
    /// <param name="fingerprint">A digest of the request. It must not contain '|'.</param>
    /// <param name="cancellationToken">Not observed: this operation completes synchronously.</param>
    /// <returns>The claim outcome.</returns>
    public Task<IdempotencyDecision> ClaimAsync(string scope, string key, string fingerprint, CancellationToken cancellationToken)
    {
        ValidateArguments(scope, key, fingerprint);

        var identity = (scope, key);
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            if (_entries.TryGetValue(identity, out var entry) && entry.ExpiresAt > now)
            {
                return Task.FromResult(Interpret(entry, fingerprint));
            }

            _entries[identity] = new Entry(fingerprint, null, now + CoordinationDefaults.IdempotencyPendingTimeToLive);
            return Task.FromResult(new IdempotencyDecision(IdempotencyOutcome.Claimed, null));
        }
    }

    /// <summary>Stores the response for a claimed key, but only while that key still holds this request's pending claim.</summary>
    /// <param name="scope">The namespace of the key.</param>
    /// <param name="key">The idempotency key.</param>
    /// <param name="fingerprint">The fingerprint the claim was made with.</param>
    /// <param name="responseJson">The response to replay.</param>
    /// <param name="cancellationToken">Not observed: this operation completes synchronously.</param>
    /// <returns>A completed task.</returns>
    public Task CompleteAsync(string scope, string key, string fingerprint, string responseJson, CancellationToken cancellationToken)
    {
        ValidateArguments(scope, key, fingerprint);
        ArgumentNullException.ThrowIfNull(responseJson);

        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            if (_entries.TryGetValue((scope, key), out var entry)
                && entry.ResponseJson is null
                && entry.ExpiresAt > now
                && string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                _entries[(scope, key)] = new Entry(fingerprint, responseJson, now + CoordinationDefaults.IdempotencyCompletedTimeToLive);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Drops a pending claim made with the same fingerprint. Completed records are never removed.</summary>
    /// <param name="scope">The namespace of the key.</param>
    /// <param name="key">The idempotency key.</param>
    /// <param name="fingerprint">The fingerprint the claim was made with.</param>
    /// <param name="cancellationToken">Not observed: this operation completes synchronously.</param>
    /// <returns>A completed task.</returns>
    public Task ReleaseAsync(string scope, string key, string fingerprint, CancellationToken cancellationToken)
    {
        ValidateArguments(scope, key, fingerprint);

        var identity = (scope, key);
        lock (_gate)
        {
            if (_entries.TryGetValue(identity, out var entry)
                && entry.ResponseJson is null
                && string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                _entries.Remove(identity);
            }
        }

        return Task.CompletedTask;
    }

    private static IdempotencyDecision Interpret(Entry entry, string fingerprint)
    {
        if (!string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return new IdempotencyDecision(IdempotencyOutcome.Conflict, null);
        }

        return entry.ResponseJson is null
            ? new IdempotencyDecision(IdempotencyOutcome.InProgress, null)
            : new IdempotencyDecision(IdempotencyOutcome.Replay, entry.ResponseJson);
    }

    private static void ValidateArguments(string scope, string key, string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        if (fingerprint.Contains('|', StringComparison.Ordinal))
        {
            throw new ArgumentException("The fingerprint must not contain '|'.", nameof(fingerprint));
        }
    }

    /// <summary>A stored record. A null <see cref="ResponseJson"/> means the claim is still pending.</summary>
    private sealed record Entry(string Fingerprint, string? ResponseJson, DateTimeOffset ExpiresAt);
}
