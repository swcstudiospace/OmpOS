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
}
