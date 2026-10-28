namespace Autonogrammer.Harness.AgentLoop.Stages;

/// <summary>Options for <see cref="UltrathinkStateObserver"/>.</summary>
public sealed record UltrathinkStateObserverOptions
{
    /// <summary>The default time between two looks at the plan file.</summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Gets the time between two looks at the plan file. It must be positive.</summary>
    public TimeSpan PollInterval { get; init; } = DefaultPollInterval;

    /// <summary>Gets a value saying whether prompt planning is switched on for the run. When false the plan stage is reported as skipped and no file is read.</summary>
    public bool PlanningEnabled { get; init; } = true;
}
