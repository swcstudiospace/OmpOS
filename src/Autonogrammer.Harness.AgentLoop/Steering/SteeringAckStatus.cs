namespace Autonogrammer.Harness.AgentLoop.Steering;

/// <summary>What the omp plugin did with a directive.</summary>
public enum SteeringAckStatus
{
    /// <summary>The plugin handed the text to the running session.</summary>
    Delivered = 0,

    /// <summary>The plugin refused the directive; <see cref="SteeringAck.Reason"/> says why.</summary>
    Rejected = 1,

    /// <summary>The plugin accepted the directive and delivery failed.</summary>
    Failed = 2,
}
