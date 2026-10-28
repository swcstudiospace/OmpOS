using OmpOS.Contracts.Sessions;

namespace OmpOS.EngineHost;

/// <summary>Raised when the connection dropped while a turn was running. The turn did not finish and is not reported as finished.</summary>
public sealed class TurnInterruptedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="TurnInterruptedEventArgs"/> class.</summary>
    /// <param name="nativeSessionId">The engine session that will be reloaded, or <see langword="null"/> when the engine never named one.</param>
    public TurnInterruptedEventArgs(string? nativeSessionId) => NativeSessionId = nativeSessionId;

    /// <summary>Gets the status the interrupted turn takes in a session record: always <see cref="SessionStatus.Interrupted"/>.</summary>
    public SessionStatus Status { get; } = SessionStatus.Interrupted;

    /// <summary>Gets the engine session that will be reloaded with <c>session/load</c>, or <see langword="null"/>.</summary>
    public string? NativeSessionId { get; }
}
