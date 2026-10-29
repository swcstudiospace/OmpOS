namespace Autonogrammer.Resilience;

/// <summary>
/// The ledger-sink defaults of D-41, bound from <see cref="SectionName"/> and validated by the API at start. The outbox row is the retry state of
/// the ledger, so the pipeline itself never retries: <see cref="MaxAttempts"/>, <see cref="FirstDelaySeconds"/> and <see cref="MaxDelaySeconds"/>
/// describe the outbox schedule the pump applies.
/// </summary>
public sealed class LedgerResilienceOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "DataPlane:Ledger:Resilience";

    /// <summary>Gets or sets the timeout of one emit in seconds (the gateway's own limit).</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Gets or sets how many times the pump tries one outbox row before it parks it.</summary>
    public int MaxAttempts { get; set; } = 8;

    /// <summary>Gets or sets the delay before the second attempt in seconds; it doubles from there.</summary>
    public int FirstDelaySeconds { get; set; } = 5;

    /// <summary>Gets or sets the longest delay between two attempts in seconds (ten minutes).</summary>
    public int MaxDelaySeconds { get; set; } = 600;

    /// <summary>Gets or sets how many emits may be in flight.</summary>
    public int InFlight { get; set; } = 2;

    /// <summary>Gets or sets how many seconds the breaker stays open.</summary>
    public int BreakerOpenSeconds { get; set; } = 60;

    /// <summary>Builds the pipeline policy: one deadline per emit, no pipeline retry, the shared in-flight limit.</summary>
    /// <param name="isTransient">The gateway's transient-failure predicate, or <see langword="null"/> for the BCL default.</param>
    /// <returns>The policy of the <c>ledger</c> component.</returns>
    public ResiliencePolicy ToPolicy(Func<Exception, bool>? isTransient = null) =>
        new()
        {
            Component = "ledger",
            InteractiveTimeout = TimeSpan.FromSeconds(TimeoutSeconds),
            BackgroundTimeout = TimeSpan.FromSeconds(TimeoutSeconds),
            MaxRetries = 0,
            InteractiveInFlight = InFlight,
            BreakerOpenDuration = TimeSpan.FromSeconds(BreakerOpenSeconds),
            IsTransient = isTransient ?? ResiliencePolicy.DefaultIsTransient,
        };
}
