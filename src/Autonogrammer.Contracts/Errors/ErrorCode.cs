namespace Autonogrammer.Contracts.Errors;

/// <summary>
/// Machine-readable error codes. <see cref="PromptInFlight"/> is required by
/// the Phase 2 session semantics (one active prompt per session).
/// </summary>
public enum ErrorCode
{
    /// <summary>Unclassified error.</summary>
    Unknown,

    /// <summary>A prompt is already running on the session.</summary>
    PromptInFlight,

    /// <summary>The session does not exist.</summary>
    SessionNotFound,

    /// <summary>An invalid harness state transition was requested.</summary>
    InvalidTransition,

    /// <summary>No sandbox provider could satisfy the request.</summary>
    SandboxUnavailable,

    /// <summary>A durable workflow run failed.</summary>
    WorkflowFailed,

    /// <summary>The operation was cancelled.</summary>
    OperationCancelled,
}
