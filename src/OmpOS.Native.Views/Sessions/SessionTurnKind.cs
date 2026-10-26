namespace OmpOS.Native.Views.Sessions;

/// <summary>What happened to the run of one agent session, as <see cref="AgentSessionRuntime.TurnChanged"/> reports it.</summary>
public enum SessionTurnKind
{
    /// <summary>A run started: the draft was sent and the engine took it.</summary>
    Started = 0,

    /// <summary>The run ended without a fault: its last turn completed, or the operator stopped it.</summary>
    Completed = 1,

    /// <summary>The run ended because a turn faulted. <see cref="SessionTurnEventArgs.Reason"/> says why.</summary>
    Faulted = 2,
}
