namespace Autonogrammer.Presentation.Runs;

/// <summary>The kind of user input that is waiting on the active run (ADE-03).</summary>
public enum PendingRunInput
{
    /// <summary>No input is waiting.</summary>
    None = 0,

    /// <summary>A steering message is being delivered to the active run.</summary>
    Steering = 1,

    /// <summary>At least one follow-up is queued until the active run completes.</summary>
    FollowUp = 2,
}
