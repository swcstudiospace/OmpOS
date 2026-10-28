namespace Autonogrammer.Contracts.Runs;

/// <summary>
/// Lifecycle states of a hosted run. A run moves from Queued to Running and ends in exactly one of Completed, Failed or Cancelled.
/// </summary>
public enum RunStatus
{
    /// <summary>The run was accepted and its harness has not started the prompt yet.</summary>
    Queued,

    /// <summary>The harness is executing the prompt, or the run is waiting for a tool-approval decision.</summary>
    Running,

    /// <summary>The prompt ran to its end and the run settled successfully.</summary>
    Completed,

    /// <summary>The run ended with a failure.</summary>
    Failed,

    /// <summary>The run was cancelled before it completed.</summary>
    Cancelled,
}
