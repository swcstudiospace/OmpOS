namespace Autonogrammer.Coordination;

/// <summary>
/// Process-local <see cref="IDispatchAuthNonces"/>. A claim is stored until its lifetime elapses on the injected clock.
/// Consuming deletes it. The nonce and its hash are never logged.
/// </summary>
public sealed class InMemoryDispatchAuthNonces : IDispatchAuthNonces
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, DateTimeOffset> _expiresAt = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Creates an empty nonce set that reads time from <paramref name="timeProvider"/>.</summary>
    /// <param name="timeProvider">The clock that decides when a claim expires.</param>
    public InMemoryDispatchAuthNonces(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task<bool> TryClaimAsync(string nonceSha256Hex, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ValidateNonce(nonceSha256Hex);
        if (timeToLive <= TimeSpan.Zero || timeToLive > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), timeToLive, "Every nonce needs a time to live of more than zero and at most 24 hours.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_expiresAt.TryGetValue(nonceSha256Hex, out var expiresAt) && expiresAt > _timeProvider.GetUtcNow())
            {
                return Task.FromResult(false);
            }

            _expiresAt[nonceSha256Hex] = _timeProvider.GetUtcNow() + timeToLive;
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<bool> TryConsumeAsync(string nonceSha256Hex, CancellationToken cancellationToken)
    {
        ValidateNonce(nonceSha256Hex);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_expiresAt.Remove(nonceSha256Hex, out var expiresAt) || expiresAt <= _timeProvider.GetUtcNow())
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
    }

    private static void ValidateNonce(string nonceSha256Hex)
    {
        ArgumentNullException.ThrowIfNull(nonceSha256Hex);
        if (nonceSha256Hex.Length != 64 || !nonceSha256Hex.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            throw new ArgumentException("The nonce hash must be 64 lowercase hexadecimal characters.", nameof(nonceSha256Hex));
        }
    }
}
