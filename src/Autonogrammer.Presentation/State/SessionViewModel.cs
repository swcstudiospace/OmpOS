using Autonogrammer.Contracts.Streaming;
using Autonogrammer.GenUi;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.State;

/// <summary>
/// The presentation state behind <c>SessionView</c>: the folded <see cref="UiTree"/>,
/// the <see cref="SessionViewState"/> lifecycle state, the scroll-pause flag, and the
/// throttled aria-live announcement text.
/// </summary>
/// <remarks>
/// Announcements are throttled by state change: streaming deltas update the tree but leave the
/// announcement untouched, so screen readers announce lifecycle transitions rather than every token.
/// </remarks>
public sealed class SessionViewModel : ObservableModel
{
    private string? _lastAnnouncedFor;
    private UiTree _tree = UiTree.Empty;
    private SessionViewState _state = SessionViewState.LoadingHistory;
    private bool _autoScrollPaused;
    private string? _announcement;
    private string? _lastError;

    /// <summary>Gets the current folded UI tree.</summary>
    public UiTree Tree { get => _tree; private set => SetProperty(ref _tree, value); }

    /// <summary>Gets the current lifecycle state.</summary>
    public SessionViewState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                NotifyPropertyChanged(nameof(CanStop));
                NotifyPropertyChanged(nameof(CanRetry));
            }
        }
    }

    /// <summary>Gets whether the user scrolled up and auto-scroll is paused.</summary>
    public bool AutoScrollPaused { get => _autoScrollPaused; private set => SetProperty(ref _autoScrollPaused, value); }

    /// <summary>Gets whether the stop control is available in the current state.</summary>
    public bool CanStop => State == SessionViewState.Streaming;

    /// <summary>Gets whether the retry affordance is available in the current state.</summary>
    public bool CanRetry => State == SessionViewState.RecoverableError;

    /// <summary>Gets the current throttled aria-live announcement, or null before the first transition.</summary>
    public string? Announcement { get => _announcement; private set => SetProperty(ref _announcement, value); }

    /// <summary>Gets the last error message, preserved with partial output on failure.</summary>
    public string? LastError { get => _lastError; private set => SetProperty(ref _lastError, value); }

    /// <summary>Folds one stream event into the tree and derives the lifecycle state.</summary>
    /// <param name="evt">The stream event.</param>
    public void ApplyEvent(StreamEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        // Diagnostics are non-terminal and have no UI surface: the live feeds drop
        // them before the view-model, so replaying a persisted log must drop them
        // here too. Folding one would add an unknown card and flush StreamingText,
        // splitting text that streamed continuously.
        if (evt.Payload is DiagnosticPayload)
        {
            return;
        }

        Tree = UiFolder.Fold(Tree, evt);
        // The server session status is the only authority for AwaitingApproval and the server
        // never sets it, so a permission event must not change the lifecycle state: the turn
        // is still streaming and the composer stays usable.
        SetState(evt.Payload switch
        {
            CompletedPayload => SessionViewState.Success,
            ErrorPayload error => error.Retryable ? SessionViewState.RecoverableError : SessionViewState.FatalError,
            _ => SessionViewState.Streaming,
        });

        if (evt.Payload is ErrorPayload failure)
        {
            LastError = failure.Message;
        }
    }

    /// <summary>Marks history as loaded: empty logs show the empty prompt, otherwise the success state.</summary>
    public void MarkHistoryLoaded()
    {
        SetState(Tree.Blocks.Count == 0 && Tree.StreamingText.Length == 0
            ? SessionViewState.Empty
            : SessionViewState.Success);
    }

    /// <summary>
    /// Enters the awaiting-approval state. Only the server session status may move the view here:
    /// permission events never call this, so the composer stays usable while a turn streams.
    /// There is deliberately no approval action on the client (Q-HARN-PERMISSION).
    /// </summary>
    public void MarkAwaitingApproval() => SetState(SessionViewState.AwaitingApproval);

    /// <summary>Marks the transport as reconnecting; output stays visible.</summary>
    public void MarkReconnecting() => SetState(SessionViewState.Reconnecting);

    /// <summary>Marks the transport as offline; output stays visible.</summary>
    public void MarkOffline() => SetState(SessionViewState.Offline);

    /// <summary>Records that the user scrolled up; auto-scroll pauses and the jump-to-latest control appears.</summary>
    public void PauseAutoScroll() => AutoScrollPaused = true;

    /// <summary>Resumes auto-scroll, typically after the user jumps to the latest output.</summary>
    public void ResumeAutoScroll() => AutoScrollPaused = false;

    private void SetState(SessionViewState state)
    {
        State = state;
        var announcement = AnnouncementFor(state);
        var key = $"{state}:{announcement}";
        if (!string.Equals(key, _lastAnnouncedFor, StringComparison.Ordinal))
        {
            _lastAnnouncedFor = key;
            Announcement = announcement;
        }
    }

    private static string AnnouncementFor(SessionViewState state) => state switch
    {
        SessionViewState.LoadingHistory => "Loading conversation history.",
        SessionViewState.Empty => "No messages yet.",
        SessionViewState.Streaming => "Assistant is responding.",
        SessionViewState.AwaitingApproval => "Approval needed to continue.",
        SessionViewState.RecoverableError => "Something went wrong. You can retry.",
        SessionViewState.FatalError => "Something went wrong.",
        SessionViewState.Reconnecting => "Reconnecting.",
        SessionViewState.Offline => "You are offline.",
        SessionViewState.Success => "Response complete.",
        _ => string.Empty,
    };
}
