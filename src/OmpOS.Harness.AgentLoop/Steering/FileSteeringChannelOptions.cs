namespace OmpOS.Harness.AgentLoop.Steering;

/// <summary>Options for <see cref="FileSteeringChannel"/>.</summary>
public sealed record FileSteeringChannelOptions
{
    /// <summary>The default time between two looks for an acknowledgement.</summary>
    public static readonly TimeSpan DefaultAckPollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>The longest heartbeat age at which the plugin counts as serving.</summary>
    public static readonly TimeSpan DefaultHeartbeatFreshness = TimeSpan.FromSeconds(15);

    /// <summary>Gets the time between two looks for an acknowledgement. It must be positive.</summary>
    public TimeSpan AckPollInterval { get; init; } = DefaultAckPollInterval;

    /// <summary>Gets how far the heartbeat may lie from the clock, in either direction, before the plugin counts as not serving. It must be positive.</summary>
    public TimeSpan HeartbeatFreshness { get; init; } = DefaultHeartbeatFreshness;
}
