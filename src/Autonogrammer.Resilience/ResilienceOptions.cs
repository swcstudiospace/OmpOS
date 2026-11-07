namespace Autonogrammer.Resilience;

/// <summary>
/// The constants every component shares: the breaker thresholds of D-41. Only the open window differs per component, so it is an option of
/// <see cref="PostgresResilienceOptions"/>, <see cref="DragonflyResilienceOptions"/> and <see cref="LedgerResilienceOptions"/>.
/// </summary>
public static class ResilienceOptions
{
    /// <summary>The number of consecutive failures that opens a breaker.</summary>
    public const int BreakerConsecutiveFailures = 5;

    /// <summary>The share of failed calls among the sampled calls that opens a breaker.</summary>
    public const double BreakerFailureRatio = 0.5;

    /// <summary>The number of most recent calls the ratio rule looks at. The rule needs this many calls inside the window.</summary>
    public const int BreakerSampledCalls = 20;

    /// <summary>How many seconds a call stays in the ratio window.</summary>
    public const int BreakerWindowSeconds = 30;
}
