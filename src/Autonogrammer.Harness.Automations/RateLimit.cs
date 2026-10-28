namespace Autonogrammer.Harness.Automations;

/// <summary>
/// Caps an automation's admitted runs at <see cref="MaxRuns"/> within any sliding <see cref="Window"/>, measured on the
/// injected clock. A run that started exactly one window earlier no longer counts.
/// </summary>
public sealed record RateLimit
{
    /// <summary>
    /// Creates a rate limit.
    /// </summary>
    /// <param name="maxRuns">The most runs admitted within one window. Must be positive.</param>
    /// <param name="window">The length of the sliding window. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">The cap or the window is not positive.</exception>
    public RateLimit(int maxRuns, TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRuns);
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "The window must be positive.");
        }

        MaxRuns = maxRuns;
        Window = window;
    }

    /// <summary>The most runs admitted within one window.</summary>
    public int MaxRuns { get; }

    /// <summary>The length of the sliding window.</summary>
    public TimeSpan Window { get; }
}
