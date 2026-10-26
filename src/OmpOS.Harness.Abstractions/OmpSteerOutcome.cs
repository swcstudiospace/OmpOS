namespace OmpOS.Harness.Abstractions;

/// <summary>How an attempt to steer the running turn of a session ended.</summary>
public enum OmpSteerOutcome
{
    /// <summary>The running turn owns the message now and delivers it at its next boundary.</summary>
    Injected,

    /// <summary>
    /// No turn took the message: the turn ended while the message was sent, or the turn is being stopped. Nothing was consumed and no turn
    /// was started, so the caller must send the text as a prompt itself.
    /// </summary>
    PromptRequired,

    /// <summary>The message was refused or could not be sent. <see cref="OmpSteerResult.Reason"/> says why.</summary>
    Failed,

    /// <summary>The engine does not offer live steering for this session. Nothing was sent.</summary>
    Unsupported,

    /// <summary>The session is closed, not open, or no turn is running. Nothing was sent.</summary>
    NotRunning,
}
