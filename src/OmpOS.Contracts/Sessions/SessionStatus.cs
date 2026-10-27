namespace OmpOS.Contracts.Sessions;

/// <summary>
/// Lifecycle states of an agent session.
/// </summary>
public enum SessionStatus
{
    /// <summary>No prompt is running.</summary>
    Idle,

    /// <summary>A prompt is currently executing.</summary>
    Running,

    /// <summary>Execution paused waiting for a tool-approval decision.</summary>
    AwaitingToolApproval,

    /// <summary>Execution finished successfully.</summary>
    Completed,

    /// <summary>Execution finished with a failure.</summary>
    Failed,

    /// <summary>Execution was cancelled.</summary>
    Cancelled,

    /// <summary>The connection to the engine dropped while a turn was running. The turn did not finish and was not cancelled; the session can be reloaded and the prompt sent again.</summary>
    Interrupted,
}
