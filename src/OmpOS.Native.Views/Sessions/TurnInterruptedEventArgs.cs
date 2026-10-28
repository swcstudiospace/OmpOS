using OmpOS.Contracts.Sessions;

namespace OmpOS.Native.Views.Sessions;

/// <summary>
/// Describes a turn that ended because the connection to the engine dropped while it ran. The conversation is safe on the engine's side; the
/// next prompt resumes it. The shell shows the session as <see cref="SessionStatus.Interrupted"/>.
/// </summary>
public sealed class TurnInterruptedEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="nativeSessionId">The identifier the engine gave the session, or null when it had not reported one.</param>
    public TurnInterruptedEventArgs(string? nativeSessionId) => NativeSessionId = nativeSessionId;

    /// <summary>Gets the status the session shows: always <see cref="SessionStatus.Interrupted"/>.</summary>
    public SessionStatus Status { get; } = SessionStatus.Interrupted;

    /// <summary>Gets the identifier the engine gave the session, or null.</summary>
    public string? NativeSessionId { get; }
}
