namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>Why the limiter refused an actuating action.</summary>
public enum RateLimitReason
{
    /// <summary>Allowed.</summary>
    None = 0,

    /// <summary>More than the per-minute cap or the burst.</summary>
    RateLimited = 1,

    /// <summary>The session step cap was reached.</summary>
    StepLimit = 2,

    /// <summary>Five consecutive failures paused the loop.</summary>
    FailurePause = 3,
}
