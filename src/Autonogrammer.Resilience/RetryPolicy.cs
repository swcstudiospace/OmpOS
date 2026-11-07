namespace Autonogrammer.Resilience;

/// <summary>
/// Decides whether a failed call is tried again and how long to wait first. The delay is decorrelated jitter: uniform between the minimum and
/// three times the previous delay, never above the maximum, so concurrent callers spread out instead of retrying in lock step. Whether the
/// total budget still has room is the pipeline's decision.
/// </summary>
internal sealed class RetryPolicy
{
    private readonly ResiliencePolicy _policy;
    private readonly Random _random;
    private readonly System.Threading.Lock _randomGate = new();

    /// <summary>Initializes a new instance of the <see cref="RetryPolicy"/> class.</summary>
    /// <param name="policy">The component policy.</param>
    /// <param name="random">The source of jitter; a seeded instance makes the schedule reproducible. Defaults to <see cref="Random.Shared"/>.</param>
    public RetryPolicy(ResiliencePolicy policy, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
        _random = random ?? Random.Shared;
    }

    /// <summary>Decides whether a failed attempt is tried again.</summary>
    /// <param name="exception">The failure of the attempt.</param>
    /// <param name="retriesDone">How many retries already ran.</param>
    /// <param name="idempotent">Whether the call is safe to run twice: a read, an idempotent statement or a write guarded by a unique key.</param>
    /// <returns><see langword="true"/> when the call is retried.</returns>
    public bool ShouldRetry(Exception exception, int retriesDone, bool idempotent)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return idempotent && retriesDone < _policy.MaxRetries && _policy.IsTransient(exception);
    }

    /// <summary>Decides whether an attempt that ran into its own deadline is tried again.</summary>
    /// <param name="retriesDone">How many retries already ran.</param>
    /// <param name="idempotent">Whether the call is safe to run twice.</param>
    /// <returns><see langword="true"/> when the call is retried.</returns>
    public bool ShouldRetryAfterTimeout(int retriesDone, bool idempotent) =>
        idempotent && retriesDone < _policy.MaxRetries;

    /// <summary>Decides whether a transaction that failed is run again from the start: only a serialisation failure, only once.</summary>
    /// <param name="exception">The failure of the attempt.</param>
    /// <param name="retriesDone">How many retries already ran.</param>
    /// <returns><see langword="true"/> when the whole transaction is retried.</returns>
    public bool ShouldRetryTransaction(Exception exception, int retriesDone)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return retriesDone < 1 && _policy.IsSerializationFailure(exception);
    }

    /// <summary>Picks the next delay.</summary>
    /// <param name="previous">The previous delay, or the minimum delay before the first retry.</param>
    /// <returns>A delay between the minimum and the maximum.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA5394:Do not use insecure randomness",
        Justification = "Randomness here is backoff jitter, not a security decision; the Random instance is injected for deterministic tests.")]
    public TimeSpan NextDelay(TimeSpan previous)
    {
        var minimum = _policy.MinDelay;
        var upper = TimeSpan.FromTicks(Math.Min(_policy.MaxDelay.Ticks, SaturatingTriple(previous.Ticks)));
        if (upper <= minimum)
        {
            return minimum;
        }

        double sample;
        lock (_randomGate)
        {
            sample = _random.NextDouble();
        }

        var ticks = minimum.Ticks + (long)(sample * (upper.Ticks - minimum.Ticks));
        return TimeSpan.FromTicks(Math.Clamp(ticks, minimum.Ticks, _policy.MaxDelay.Ticks));
    }

    private static long SaturatingTriple(long ticks) => ticks > long.MaxValue / 3 ? long.MaxValue : ticks * 3;
}
