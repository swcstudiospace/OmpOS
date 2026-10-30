namespace Autonogrammer.Harness.Acp;

/// <summary>How a <see cref="AcpAgentSession.SteerAsync"/> attempt ended.</summary>
public enum AcpSteerOutcome
{
    /// <summary>omp answered <c>injected</c>: the running turn owns the message and delivers it at its next boundary.</summary>
    Injected,

    /// <summary>
    /// omp answered <c>promptRequired</c>: the session is idle, the turn settled while the message was admitted, or the turn is being aborted.
    /// Nothing was consumed and no turn was started: the caller must send the text through <c>session/prompt</c> itself.
    /// </summary>
    PromptRequired,

    /// <summary>The text was refused before the call, omp answered <c>failed</c>, or omp answered with a JSON-RPC error. <see cref="AcpSteerResult.Reason"/> says why.</summary>
    Failed,

    /// <summary>omp did not advertise <c>_meta.steering.supported</c> in <c>initialize</c>. Nothing was sent.</summary>
    Unsupported,

    /// <summary>The session is closed or not open, or this client has no prompt in flight. Nothing was sent.</summary>
    NotRunning,
}
