namespace OmpOS.Contracts.Agents;

/// <summary>
/// The component of the agent loop that produced a <see cref="StageEvent"/>. The order of the members is the pipeline order:
/// the planner runs first, the orchestrator second, and the substrate and the workflow engine run alongside them.
/// </summary>
public enum StageSource
{
    /// <summary>The omp engine itself: the session, the turn and the model run.</summary>
    Omp,

    /// <summary>The prompt planner: the XML spec, the Graph of Thought and the workflow waves.</summary>
    Ultrathink,

    /// <summary>The subagent orchestrator that runs after the planner.</summary>
    Swarm,

    /// <summary>The session substrate: shared memory, leases, handoffs and the event ledger.</summary>
    Substrate,

    /// <summary>The workflow engine whose phases and plans the session works through.</summary>
    Gsd,
}
