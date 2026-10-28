using OmpOS.Contracts.Environments;

namespace OmpOS.Native.Views.Sessions;

/// <summary>Describes a change of the connection of a remote session: it is connecting, connected, reconnecting or unreachable.</summary>
public sealed class SessionConnectionChangedEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="state">The new connection state.</param>
    /// <param name="attempt">The reconnect attempt the state belongs to, or zero.</param>
    /// <param name="lastFailure">The plain reason of the last failed attempt, or null.</param>
    public SessionConnectionChangedEventArgs(RemoteConnectionState state, int attempt, string? lastFailure)
    {
        State = state;
        Attempt = attempt;
        LastFailure = lastFailure;
    }

    /// <summary>Gets the new connection state.</summary>
    public RemoteConnectionState State { get; }

    /// <summary>Gets the reconnect attempt the state belongs to, or zero.</summary>
    public int Attempt { get; }

    /// <summary>Gets the plain reason of the last failed attempt, or null.</summary>
    public string? LastFailure { get; }
}
