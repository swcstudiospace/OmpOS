namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>The load state of the task context of the selected session.</summary>
public enum TaskContextState
{
    /// <summary>The context is loading. The tab shows static text, never a spinner.</summary>
    Loading = 0,

    /// <summary>The context loaded with no warning.</summary>
    Ready = 1,

    /// <summary>The context loaded but part of it could not be read. The warnings say which.</summary>
    Partial = 2,

    /// <summary>There is no context to show: the agent has no task, or the source failed or was cancelled.</summary>
    Unavailable = 3,
}
