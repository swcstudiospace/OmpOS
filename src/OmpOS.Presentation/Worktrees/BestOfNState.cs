namespace OmpOS.Presentation.Worktrees;

/// <summary>Where a best-of-n candidate stands.</summary>
public enum BestOfNState
{
    /// <summary>The worktree exists and the agent has not started.</summary>
    Waiting = 0,

    /// <summary>The agent is working in the worktree.</summary>
    Running = 1,

    /// <summary>The agent finished its turn.</summary>
    Finished = 2,

    /// <summary>The worktree could not be made, or the agent did not finish.</summary>
    Failed = 3,
}
