using OmpOS.Contracts.Environments;

namespace OmpOS.EngineHost;

/// <summary>Describes a change of the connection state of a <see cref="ReconnectingEngineHost"/>.</summary>
public sealed class ConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ConnectionStateChangedEventArgs"/> class.</summary>
    /// <param name="state">The new state.</param>
    /// <param name="attempt">The reconnect attempt the state belongs to, or 0 when no attempt is under way.</param>
    /// <param name="lastFailure">The message of the failure that preceded the state, or <see langword="null"/>.</param>
    public ConnectionStateChangedEventArgs(RemoteConnectionState state, int attempt, string? lastFailure)
    {
        State = state;
        Attempt = attempt;
        LastFailure = lastFailure;
    }

    /// <summary>Gets the new state.</summary>
    public RemoteConnectionState State { get; }

    /// <summary>Gets the reconnect attempt the state belongs to, or 0 when no attempt is under way.</summary>
    public int Attempt { get; }

    /// <summary>Gets the message of the failure that preceded the state, or <see langword="null"/>. It holds no secret.</summary>
    public string? LastFailure { get; }
}
