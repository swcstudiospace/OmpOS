namespace OmpOS.Harness.AgentLoop.Stages;

/// <summary>Options for <see cref="SwarmLiveObserver"/>.</summary>
public sealed record SwarmLiveObserverOptions
{
    /// <summary>The default time between two looks at the swarm files.</summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Gets the time between two looks at the swarm files. It must be positive.</summary>
    public TimeSpan PollInterval { get; init; } = DefaultPollInterval;
}
