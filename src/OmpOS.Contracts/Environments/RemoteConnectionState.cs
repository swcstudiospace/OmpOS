namespace OmpOS.Contracts.Environments;

/// <summary>The state of the connection between this machine and the remote omp of one session.</summary>
public enum RemoteConnectionState
{
    /// <summary>No connection has been opened yet.</summary>
    Disconnected,

    /// <summary>The first connection is being opened.</summary>
    Connecting,

    /// <summary>The connection is up and the session is loaded.</summary>
    Connected,

    /// <summary>The connection dropped and the client is retrying with a growing delay.</summary>
    Reconnecting,

    /// <summary>Every retry failed. A new prompt starts another round of retries.</summary>
    Unreachable,

    /// <summary>The session was closed on purpose.</summary>
    Closed,
}
