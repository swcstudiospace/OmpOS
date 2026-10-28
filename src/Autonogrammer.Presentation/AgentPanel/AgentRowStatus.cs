namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>
/// The one status a rail row and a thread header chip show for a session. It combines the persisted lifecycle, the live runtime status and
/// whether the session was closed; <see cref="AgentRowStatusMap"/> is the only place that decides it. The status is always shown as words and a glyph.
/// </summary>
public enum AgentRowStatus
{
    /// <summary>The lifecycle or runtime status is not known to this build. The row reads <c>Status unknown</c> and never throws.</summary>
    Unknown = 0,

    /// <summary>A tool approval waits for the user. Group <see cref="AgentRowGroup.NeedsYou"/>.</summary>
    NeedsApproval = 1,

    /// <summary>The agent waits for the user's next message. Group <see cref="AgentRowGroup.NeedsYou"/>.</summary>
    WaitingForYou = 2,

    /// <summary>A run is executing. Group <see cref="AgentRowGroup.Running"/>.</summary>
    Running = 3,

    /// <summary>A run was accepted and has not started. Group <see cref="AgentRowGroup.Running"/>.</summary>
    Queued = 4,

    /// <summary>The session was running when the application closed, or its engine cannot be reached. Group <see cref="AgentRowGroup.Lost"/>.</summary>
    Lost = 5,

    /// <summary>The session exists and has not run yet. Group <see cref="AgentRowGroup.Idle"/>.</summary>
    Ready = 6,

    /// <summary>The session is paused. Group <see cref="AgentRowGroup.Idle"/>.</summary>
    Paused = 7,

    /// <summary>The operator closed the session: its engine stopped. Group <see cref="AgentRowGroup.Idle"/>.</summary>
    Closed = 8,

    /// <summary>The last run finished successfully. Group <see cref="AgentRowGroup.Finished"/>.</summary>
    Completed = 9,

    /// <summary>The last run failed. Group <see cref="AgentRowGroup.Finished"/>.</summary>
    Failed = 10,
}
