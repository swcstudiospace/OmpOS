namespace Autonogrammer.Resilience;

/// <summary>The Dragonfly defaults of D-41, bound from <see cref="SectionName"/> and validated by the API at start.</summary>
public sealed class DragonflyResilienceOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "DataPlane:Dragonfly:Resilience";

    /// <summary>Gets or sets the timeout of one operation in milliseconds (the client's <c>asyncTimeout</c>).</summary>
    public int OpMs { get; set; } = 250;

    /// <summary>Gets or sets the connect timeout in seconds (the client's <c>connectTimeout</c>).</summary>
    public int ConnectSeconds { get; set; } = 2;

    /// <summary>Gets or sets how many times a read or an idempotent script is retried.</summary>
    public int MaxRetries { get; set; } = 1;

    /// <summary>Gets or sets the lower bound of the retry delay in milliseconds.</summary>
    public int MinDelayMs { get; set; } = 20;

    /// <summary>Gets or sets the upper bound of the retry delay in milliseconds.</summary>
    public int MaxDelayMs { get; set; } = 80;

    /// <summary>Gets or sets how many calls may be in flight on the one multiplexer.</summary>
    public int InFlight { get; set; } = 64;

    /// <summary>Gets or sets how many seconds the breaker stays open.</summary>
    public int BreakerOpenSeconds { get; set; } = 10;

    /// <summary>Builds the pipeline policy. The total deadline is derived: two attempts at <see cref="OpMs"/> plus one maximum delay.</summary>
    /// <param name="isTransient">The driver's transient-failure predicate, or <see langword="null"/> for the BCL default.</param>
    /// <returns>The policy of the <c>dragonfly</c> component.</returns>
    public ResiliencePolicy ToPolicy(Func<Exception, bool>? isTransient = null) =>
        new()
        {
            Component = "dragonfly",
            InteractiveTimeout = TimeSpan.FromMilliseconds(OpMs),
            BackgroundTimeout = TimeSpan.FromMilliseconds(OpMs),
            MaxRetries = MaxRetries,
            MinDelay = TimeSpan.FromMilliseconds(MinDelayMs),
            MaxDelay = TimeSpan.FromMilliseconds(MaxDelayMs),
            InteractiveInFlight = InFlight,
            BreakerOpenDuration = TimeSpan.FromSeconds(BreakerOpenSeconds),
            IsTransient = isTransient ?? ResiliencePolicy.DefaultIsTransient,
        };
}
