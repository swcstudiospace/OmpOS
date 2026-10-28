namespace Autonogrammer.Presentation.Kanban;

/// <summary>
/// The runtime status a Kanban card shows. Every value is set from a runtime signal through <see cref="RuntimeStatusMap"/>; the
/// view never sets one. Staleness is not a status: it is carried separately by <see cref="KanbanCardState.IsStale"/>.
/// </summary>
public enum RuntimeStatus
{
    /// <summary>Accepted by the runtime and not yet started.</summary>
    Queued,

    /// <summary>Executing.</summary>
    Running,

    /// <summary>Waiting for the user's next message.</summary>
    AwaitingInput,

    /// <summary>Waiting for a user decision that is not a message reply, such as a tool approval.</summary>
    NeedsAttention,

    /// <summary>Paused by the runtime or by the user.</summary>
    Paused,

    /// <summary>Finished successfully.</summary>
    Completed,

    /// <summary>Finished with a failure.</summary>
    Failed,

    /// <summary>The runtime session cannot be reached.</summary>
    Unreachable,
}
