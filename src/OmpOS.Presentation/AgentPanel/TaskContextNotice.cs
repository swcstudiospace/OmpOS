namespace OmpOS.Presentation.AgentPanel;

/// <summary>Which heading and body the Context tab shows above or instead of the task card.</summary>
public enum TaskContextNotice
{
    /// <summary>No notice: the task card and lists are all there is.</summary>
    None = 0,

    /// <summary>The context is loading.</summary>
    Loading = 1,

    /// <summary>The agent is not bound to a task.</summary>
    NoTask = 2,

    /// <summary>Substrate is not reachable, so shared memory and lessons are off. The task card still shows what was read.</summary>
    SubstrateLimited = 3,

    /// <summary>The source failed or was cancelled.</summary>
    Failed = 4,

    /// <summary>Part of the context could not be read.</summary>
    Partial = 5,
}
