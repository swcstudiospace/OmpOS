namespace Autonogrammer.Contracts.Cowork;

/// <summary>
/// The lifecycle a Cowork task reports. It is not a Code session lifecycle and is never stored as one.
/// </summary>
public enum CoworkSessionStatus
{
    /// <summary>The task exists and has not started a turn.</summary>
    Created,

    /// <summary>A turn is running.</summary>
    Running,

    /// <summary>
    /// A tool approval is waiting for the person. This is the data source of the hidden-surface chip
    /// "Cowork, needs approval". The approval queue that feeds it belongs to a later phase.
    /// </summary>
    WaitingForDecision,

    /// <summary>The person paused the task.</summary>
    Paused,

    /// <summary>The task finished its work.</summary>
    Completed,

    /// <summary>The task ended with a failure.</summary>
    Failed,

    /// <summary>The task was running when the application last ended, and no engine is attached to it now.</summary>
    Lost,
}
