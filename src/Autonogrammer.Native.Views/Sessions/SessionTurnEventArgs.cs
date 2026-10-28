namespace Autonogrammer.Native.Views.Sessions;

/// <summary>Describes one change of the run of an agent session: it started, completed or faulted.</summary>
public sealed class SessionTurnEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="kind">What happened to the run.</param>
    /// <param name="reason">The fault's message for <see cref="SessionTurnKind.Faulted"/>; null otherwise.</param>
    public SessionTurnEventArgs(SessionTurnKind kind, string? reason = null)
    {
        Kind = kind;
        Reason = reason;
    }

    /// <summary>Gets what happened to the run.</summary>
    public SessionTurnKind Kind { get; }

    /// <summary>Gets the fault's message when <see cref="Kind"/> is <see cref="SessionTurnKind.Faulted"/>; otherwise null.</summary>
    public string? Reason { get; }
}
