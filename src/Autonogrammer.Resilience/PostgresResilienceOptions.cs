namespace Autonogrammer.Resilience;

/// <summary>The PostgreSQL defaults of D-41, bound from <see cref="SectionName"/> and validated by the API at start.</summary>
public sealed class PostgresResilienceOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "DataPlane:Postgres:Resilience";

    /// <summary>Gets or sets the connect timeout in seconds (the connection string's <c>Timeout</c>).</summary>
    public int ConnectSeconds { get; set; } = 5;

    /// <summary>Gets or sets the command timeout of an interactive call in seconds.</summary>
    public int InteractiveCommandSeconds { get; set; } = 5;

    /// <summary>Gets or sets the command timeout of a background call in seconds.</summary>
    public int BackgroundCommandSeconds { get; set; } = 30;

    /// <summary>Gets or sets how many times a read or an idempotent statement is retried.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>Gets or sets the lower bound of the retry delay in milliseconds.</summary>
    public int MinDelayMs { get; set; } = 100;

    /// <summary>Gets or sets the upper bound of the retry delay in milliseconds.</summary>
    public int MaxDelayMs { get; set; } = 2000;

    /// <summary>Gets or sets the total time of a call and its retries in seconds. It may not exceed twice the command timeout.</summary>
    public int BudgetSeconds { get; set; } = 8;

    /// <summary>Gets or sets how many interactive calls may be in flight (the interactive share of <c>Maximum Pool Size=20</c>).</summary>
    public int PoolInteractive { get; set; } = 16;

    /// <summary>Gets or sets how many background calls may be in flight (the background share of <c>Maximum Pool Size=20</c>).</summary>
    public int PoolBackground { get; set; } = 4;

    /// <summary>Gets or sets how many seconds the breaker stays open.</summary>
    public int BreakerOpenSeconds { get; set; } = 15;

    /// <summary>Builds the pipeline policy.</summary>
    /// <param name="isTransient">The driver's transient-failure predicate, or <see langword="null"/> for the BCL default.</param>
    /// <param name="isSerializationFailure">The driver's serialisation-failure predicate, or <see langword="null"/> for none.</param>
    /// <returns>The policy of the <c>postgres</c> component.</returns>
    public ResiliencePolicy ToPolicy(Func<Exception, bool>? isTransient = null, Func<Exception, bool>? isSerializationFailure = null) =>
        new()
        {
            Component = "postgres",
            InteractiveTimeout = TimeSpan.FromSeconds(InteractiveCommandSeconds),
            BackgroundTimeout = TimeSpan.FromSeconds(BackgroundCommandSeconds),
            MaxRetries = MaxRetries,
            MinDelay = TimeSpan.FromMilliseconds(MinDelayMs),
            MaxDelay = TimeSpan.FromMilliseconds(MaxDelayMs),
            Budget = TimeSpan.FromSeconds(BudgetSeconds),
            InteractiveInFlight = PoolInteractive,
            BackgroundInFlight = PoolBackground,
            BreakerOpenDuration = TimeSpan.FromSeconds(BreakerOpenSeconds),
            IsTransient = isTransient ?? ResiliencePolicy.DefaultIsTransient,
            IsSerializationFailure = isSerializationFailure ?? (static _ => false),
        };
}
