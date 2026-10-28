namespace Autonogrammer.Presentation.Agents;

/// <summary>The state of one agent run as the Agents window shows it (ADE-05).</summary>
public enum AgentRunStatus
{
    /// <summary>The run is executing.</summary>
    Running = 0,

    /// <summary>The run finished its work.</summary>
    Completed = 1,

    /// <summary>The run stopped on an error.</summary>
    Failed = 2,

    /// <summary>The run was stopped before it finished.</summary>
    Cancelled = 3,
}
