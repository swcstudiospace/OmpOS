namespace Autonogrammer.Presentation.Subagents;

/// <summary>The status of one omp task call, as the sub-agent panel shows it.</summary>
public enum SubagentStatus
{
    /// <summary>The task call is still running.</summary>
    Running,

    /// <summary>The task call finished.</summary>
    Done,

    /// <summary>The task call failed.</summary>
    Failed,

    /// <summary>The task call was cancelled.</summary>
    Cancelled,
}
