namespace Autonogrammer.Harness.AgentLoop.Stages;

/// <summary>
/// The phase of one subagent run, normalised from the swarm run-state vocabulary (<c>swarm.live.v1</c>: waiting, queued,
/// starting, running, handing_off, completed, failed, cancelled, timed_out) and from the Task Store vocabulary that
/// <c>subagent.status.stream</c> events use.
/// </summary>
internal enum SwarmRunPhase
{
    /// <summary>The run waits for a dependency or an input.</summary>
    Waiting,

    /// <summary>The run waits for a free slot.</summary>
    Queued,

    /// <summary>The run has a slot and its process is starting.</summary>
    Starting,

    /// <summary>The run is working.</summary>
    Running,

    /// <summary>The run hands its result to the next agent.</summary>
    HandingOff,

    /// <summary>The run finished successfully.</summary>
    Completed,

    /// <summary>The run failed.</summary>
    Failed,

    /// <summary>The run was cancelled.</summary>
    Cancelled,

    /// <summary>The run exceeded its time budget.</summary>
    TimedOut,

    /// <summary>A state word this build does not know. It is shown as running with the raw word as the detail.</summary>
    Unknown,
}
