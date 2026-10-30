namespace Autonogrammer.Contracts.Agents;

/// <summary>
/// The persisted lifecycle of an agent session. It is coarser than the live run state: it survives an application restart.
/// </summary>
public enum SessionLifecycle
{
    /// <summary>The session exists and has not run a turn.</summary>
    Created,

    /// <summary>A turn is running, or the session is waiting for input or an approval.</summary>
    Running,

    /// <summary>The operator paused the session.</summary>
    Paused,

    /// <summary>The session finished its work.</summary>
    Completed,

    /// <summary>The session ended with a failure.</summary>
    Failed,

    /// <summary>The session was running when the application last ended, and no engine is attached to it now. It can be resumed from its stored reference.</summary>
    Lost,
}
