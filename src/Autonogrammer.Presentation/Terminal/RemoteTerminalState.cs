namespace Autonogrammer.Presentation.Terminal;

/// <summary>Where a remote terminal attach stands.</summary>
public enum RemoteTerminalState
{
    /// <summary>No remote terminal is attached and none was lost.</summary>
    Detached,

    /// <summary>The output stream is open.</summary>
    Attached,

    /// <summary>The connection broke or the stream ended; attaching again resumes after the last accepted chunk.</summary>
    ConnectionLost,
}
