using System.Globalization;

namespace Autonogrammer.Coordination;

/// <summary>
/// The replica-local rate limiter that stands in when the shared limiter in Dragonfly is unreachable. It keeps one bucket of
/// <c>limit</c> tokens per policy, tenant and window, and drops a bucket five seconds after its window ends, the lifetime the
/// shared key has. It is never unlimited: a non-positive limit is rejected, so an outage degrades the limit to one per replica and
/// never removes it.
/// </summary>
public sealed class InProcessRateLimiter
{
    /// <summary>How long a bucket outlives its window, the same grace as the shared <c>rl</c> key.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);
    private DateTimeOffset _nextSweep;

    /// <summary>Creates a limiter that reads time from <paramref name="timeProvider"/>.</summary>
    /// <param name="timeProvider">The clock; tests pass a manual one.</param>
    public InProcessRateLimiter(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>Takes one token from the bucket of the current window.</summary>
    /// <param name="policy">The rate-limit policy name.</param>
    /// <param name="tenant">The tenant segment of the caller.</param>
    /// <param name="window">The window length. Windows are aligned to the UTC epoch.</param>
    /// <param name="limit">How many tokens a window holds. Must be positive.</param>
    /// <returns><see langword="true"/> when a token was available; <see langword="false"/> when the caller is over the limit.</returns>
    public bool TryAcquire(string policy, string tenant, TimeSpan window, int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(limit, 0);

        var now = _timeProvider.GetUtcNow();
        var index = now.UtcTicks / window.Ticks;
        var expiresAt = new DateTimeOffset(((index + 1) * window.Ticks) + Grace.Ticks, TimeSpan.Zero);
        var key = string.Join('\n', policy, tenant, window.Ticks.ToString(CultureInfo.InvariantCulture), index.ToString(CultureInfo.InvariantCulture));

        lock (_gate)
        {
            Sweep(now);
            if (!_buckets.TryGetValue(key, out var bucket))
            {
                bucket = new Bucket(expiresAt);
                _buckets[key] = bucket;
            }

            if (bucket.Used >= limit)
            {
                return false;
            }

            bucket.Used++;
            return true;
        }
    }

    private void Sweep(DateTimeOffset now)
    {
        if (now < _nextSweep)
        {
            return;
        }

        foreach (var expired in _buckets.Where(pair => pair.Value.ExpiresAt <= now).Select(pair => pair.Key).ToArray())
        {
            _ = _buckets.Remove(expired);
        }

        _nextSweep = now + Grace;
    }

    private sealed class Bucket(DateTimeOffset expiresAt)
    {
        public DateTimeOffset ExpiresAt { get; } = expiresAt;

        public int Used { get; set; }
    }
}
