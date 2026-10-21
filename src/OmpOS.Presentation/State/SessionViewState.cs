namespace OmpOS.Presentation.State;

/// <summary>
/// The streaming lifecycle state of one chat session view (UI-02).
/// </summary>
/// <remarks>
/// This enum is the single source of truth for the throttled aria-live announcements
/// and for the stop and retry affordances: <see cref="Streaming"/> shows the stop control,
/// <see cref="RecoverableError"/> shows the retry affordance while preserving partial output,
/// and every other state shows neither.
/// </remarks>
public enum SessionViewState
{
    /// <summary>History is still loading; the view shows a loading placeholder.</summary>
    LoadingHistory = 0,

    /// <summary>No messages yet; the view shows the empty prompt.</summary>
    Empty = 1,

    /// <summary>Assistant output is streaming; the stop control is available.</summary>
    Streaming = 2,

    /// <summary>The agent waits for the user to approve a permission request.</summary>
    AwaitingApproval = 3,

    /// <summary>A retryable failure occurred; partial output is preserved and retry is offered.</summary>
    RecoverableError = 4,

    /// <summary>An unrecoverable failure occurred; partial output is preserved without retry.</summary>
    FatalError = 5,

    /// <summary>The transport is reconnecting; the last known output stays visible.</summary>
    Reconnecting = 6,

    /// <summary>The transport is offline; the last known output stays visible.</summary>
    Offline = 7,

    /// <summary>The turn completed successfully.</summary>
    Success = 8,
}
