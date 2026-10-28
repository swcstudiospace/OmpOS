namespace Autonogrammer.Contracts.Workflows;

/// <summary>
/// Lifecycle states of a durable session run.
/// </summary>
public enum WorkflowRunStatus
{
    /// <summary>Run created but not started.</summary>
    NotStarted,

    /// <summary>Run executing.</summary>
    Running,

    /// <summary>Run finished successfully.</summary>
    Completed,

    /// <summary>Run finished with a failure.</summary>
    Failed,

    /// <summary>Run was cancelled.</summary>
    Cancelled,
}
