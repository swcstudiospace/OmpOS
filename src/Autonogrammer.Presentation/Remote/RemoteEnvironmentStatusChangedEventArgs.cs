namespace Autonogrammer.Presentation.Remote;

/// <summary>Arguments of an event that reports the connection status of the remote environment a session runs on.</summary>
public sealed class RemoteEnvironmentStatusChangedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="RemoteEnvironmentStatusChangedEventArgs"/> class.</summary>
    /// <param name="status">The status, or <see langword="null"/> when the connection is healthy and the rail shows the session's own status again.</param>
    public RemoteEnvironmentStatusChangedEventArgs(RemoteEnvironmentStatus? status)
    {
        Status = status;
    }

    /// <summary>Gets the status, or <see langword="null"/> when the connection is healthy.</summary>
    public RemoteEnvironmentStatus? Status { get; }
}
