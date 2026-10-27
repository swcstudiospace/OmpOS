namespace OmpOS.EngineHost;

/// <summary>
/// How a remote connection is retried after it drops: a fixed number of attempts, each after a delay that doubles from
/// <see cref="InitialDelay"/> up to <see cref="MaxDelay"/>. The defaults are 1, 2, 4, 8 and 16 seconds, then the host is reported
/// unreachable. The policy computes delays only; the waiting is done by the host through its <see cref="TimeProvider"/>.
/// </summary>
public sealed record ReconnectPolicy
{
    /// <summary>The largest accepted <see cref="MaxAttempts"/>.</summary>
    public const int MaxAttemptsLimit = 100;

    /// <summary>Gets the default policy: five attempts after 1, 2, 4, 8 and 16 seconds.</summary>
    public static ReconnectPolicy Default { get; } = new();

    /// <summary>Gets the delay before the first attempt. It doubles for each further attempt. Defaults to one second.</summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets the longest delay before any attempt. Defaults to 30 seconds.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets how many attempts one round makes before the host is reported unreachable. Defaults to five.</summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>Returns the delay to wait before an attempt.</summary>
    /// <param name="attempt">The attempt number, starting at 1.</param>
    /// <returns>The delay: <see cref="InitialDelay"/> doubled for each earlier attempt, never more than <see cref="MaxDelay"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The attempt is below 1 or above <see cref="MaxAttempts"/>.</exception>
    public TimeSpan DelayBefore(int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(attempt, MaxAttempts);
        var ticks = InitialDelay.Ticks;
        for (var step = 1; step < attempt && ticks < MaxDelay.Ticks; step++)
        {
            ticks = Math.Min(ticks * 2, MaxDelay.Ticks);
        }

        return TimeSpan.FromTicks(Math.Min(ticks, MaxDelay.Ticks));
    }

    /// <summary>Checks the policy.</summary>
    /// <exception cref="ArgumentException">A delay is not positive, the maximum delay is below the initial delay, or the attempt count is outside 1 to <see cref="MaxAttemptsLimit"/>.</exception>
    public void Validate()
    {
        if (InitialDelay <= TimeSpan.Zero)
        {
            throw new ArgumentException("The initial delay must be positive.", nameof(InitialDelay));
        }

        if (MaxDelay < InitialDelay)
        {
            throw new ArgumentException("The maximum delay must not be below the initial delay.", nameof(MaxDelay));
        }

        if (MaxAttempts is < 1 or > MaxAttemptsLimit)
        {
            throw new ArgumentException("The attempt count must be between 1 and 100.", nameof(MaxAttempts));
        }
    }
}
