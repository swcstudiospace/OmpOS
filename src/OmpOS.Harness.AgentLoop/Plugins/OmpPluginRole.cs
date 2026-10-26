namespace OmpOS.Harness.AgentLoop.Plugins;

/// <summary>
/// What an omp plugin does for the agent loop. The role decides which stage source reports on the plugin.
/// </summary>
public enum OmpPluginRole
{
    /// <summary>Rewrites the prompt into a specification, a graph of thought and workflow waves.</summary>
    PromptPlanner,

    /// <summary>Runs subagents after the planner.</summary>
    SubagentOrchestrator,

    /// <summary>Shares memory, leases, handoffs and the event ledger across sessions.</summary>
    SessionSubstrate,

    /// <summary>Drives the phases and plans the session works through.</summary>
    WorkflowEngine,
}
