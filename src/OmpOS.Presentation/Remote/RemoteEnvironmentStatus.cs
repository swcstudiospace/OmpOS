namespace OmpOS.Presentation.Remote;

/// <summary>Where a remote environment stands: the outcome of its last connection test, or the live state of the session running on it.</summary>
public enum RemoteEnvironmentStatus
{
    /// <summary>No test was run and no session is connected.</summary>
    NotChecked,

    /// <summary>A connection test is running.</summary>
    Testing,

    /// <summary>The host key is not trusted yet: the user confirms it in the interactive terminal (an action is offered).</summary>
    HostKeyUnconfirmed,

    /// <summary>The host key changed: nothing is offered but the explanation.</summary>
    HostKeyChanged,

    /// <summary>The connection is open and ssh is signing in.</summary>
    Authenticating,

    /// <summary>The first connection of a session is being opened.</summary>
    Connecting,

    /// <summary>A session is connected.</summary>
    Connected,

    /// <summary>The connection dropped and is being opened again.</summary>
    Reconnecting,

    /// <summary>The host refused the credentials ssh could offer without a prompt.</summary>
    AuthFailed,

    /// <summary>The host cannot be reached or ssh is missing.</summary>
    Unreachable,

    /// <summary>omp or Bun is missing on the host, or omp is older than OmpOS supports.</summary>
    VersionMismatch,

    /// <summary>Another setting of the host must be fixed (shell, folder, tools).</summary>
    NeedsSetup,

    /// <summary>The last test passed.</summary>
    Ready,

    /// <summary>The last test passed with warnings.</summary>
    ReadyWithWarnings,

    /// <summary>The session on this host was closed.</summary>
    Disconnected,

    /// <summary>The connection was lost while an agent turn was running: the turn did not finish and the prompt has to be sent again.</summary>
    Interrupted,
}
