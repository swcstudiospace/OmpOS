namespace OmpOS.Harness.Abstractions;

/// <summary>
/// Lifecycle states of an omp harness session. Legal moves are defined by
/// <see cref="HarnessStateMachine.AllowedTransitions"/>.
/// </summary>
public enum HarnessState
{
    /// <summary>No transport is open.</summary>
    Disconnected,

    /// <summary>The engine process or transport is being started.</summary>
    Connecting,

    /// <summary>The ACP or RPC handshake is in progress.</summary>
    Handshaking,

    /// <summary>The session is idle and accepts prompts.</summary>
    Ready,

    /// <summary>A prompt is streaming events.</summary>
    Streaming,

    /// <summary>A cancellation was requested and the stream is winding down.</summary>
    Cancelling,

    /// <summary>The session failed and must be re-created before reuse.</summary>
    Faulted,

    /// <summary>The session is permanently closed.</summary>
    Disposed,
}
