namespace Autonogrammer.Presentation.Kanban;

/// <summary>
/// The closed set of runtime facts that can set a card's status. Each kind names one runtime event; the only mapping from a kind
/// to a <see cref="RuntimeStatus"/> is <see cref="RuntimeStatusMap"/>. Concrete signal records arrive with their runtime contracts.
/// </summary>
public enum RuntimeSignalKind
{
    /// <summary>A run was accepted and is waiting to start.</summary>
    RunQueued,

    /// <summary>A run started executing a prompt.</summary>
    RunStarted,

    /// <summary>The runtime is waiting for the user's next message.</summary>
    InputRequested,

    /// <summary>The runtime is paused for a tool-approval decision.</summary>
    ApprovalRequested,

    /// <summary>The runtime reports that the run is paused.</summary>
    RunPaused,

    /// <summary>A run finished successfully.</summary>
    RunCompleted,

    /// <summary>A run finished with a failure.</summary>
    RunFailed,

    /// <summary>The runtime session is disconnected or otherwise unreachable.</summary>
    SessionUnreachable,
}
