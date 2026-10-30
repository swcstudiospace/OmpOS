#pragma warning disable MA0048 // The plan owns this file for the state enum and its sentences.
namespace Autonogrammer.Contracts.Cowork.Browser;

/// <summary>
/// What the pane is showing. Web and Android stay on <see cref="RemoteViewUnavailable"/> because no relay feed is built.
/// </summary>
public enum BrowserPaneState
{
    /// <summary>No viewer is attached.</summary>
    Idle,

    /// <summary>The screencast is starting.</summary>
    Starting,

    /// <summary>Frames are arriving.</summary>
    Live,

    /// <summary>The user holds the pointer and the keyboard.</summary>
    UserControl,

    /// <summary>The browser process has ended.</summary>
    Stopped,

    /// <summary>This head cannot show the desktop browser.</summary>
    RemoteViewUnavailable,

    /// <summary>Browser use is turned off.</summary>
    BrowserDisabled,

    /// <summary>Chromium is not installed.</summary>
    BrowserNotInstalled,

    /// <summary>The managed browser is not pinned.</summary>
    ManagedBrowserNotPinned,

    /// <summary>Chromium is older than the pin.</summary>
    BrowserTooOld,

    /// <summary>The sandbox cannot start.</summary>
    SandboxUnavailable,

    /// <summary>The profile path is not safe.</summary>
    ProfilePathUnsafe,

    /// <summary>Required fonts are missing.</summary>
    FontsMissing,

    /// <summary>The process failed to launch.</summary>
    LaunchFailed,

    /// <summary>The process crashed.</summary>
    Crashed,

    /// <summary>The user has control, so the agent is waiting.</summary>
    UserHasControl,

    /// <summary>Stop revoked control until the user resumes.</summary>
    ControlRevoked,

    /// <summary>The current model cannot read images.</summary>
    ModelCannotSeeImages,

    /// <summary>The session image budget is spent.</summary>
    ImageBudgetExhausted,

    /// <summary>An element ref no longer matches the page.</summary>
    StaleRef,

    /// <summary>The origin was denied.</summary>
    OriginDenied,

    /// <summary>Policy blocked the navigation.</summary>
    BlockedByPolicy,

    /// <summary>The download was denied.</summary>
    DownloadDenied,

    /// <summary>A popup was closed.</summary>
    PopupClosed,

    /// <summary>A dialog was dismissed.</summary>
    DialogDismissed,

    /// <summary>The page is too large to read.</summary>
    PageTooLarge,

    /// <summary>Navigation did not finish in time.</summary>
    NavigationTimeout,

    /// <summary>Actions are rate limited.</summary>
    RateLimited,

    /// <summary>The session cap is reached.</summary>
    SessionLimitReached,

    /// <summary>The egress guard is not running.</summary>
    EgressGuardUnavailable,

    /// <summary>A local person must approve, and none is present.</summary>
    NoLocalApprover,
}

/// <summary>One sentence and one fix action for each <see cref="BrowserPaneState"/>.</summary>
public static class BrowserPaneCopy
{
    /// <summary>The sentence Web and Android show instead of a frame.</summary>
    public const string RemoteViewSentence = "This browser runs on your desktop. Open Autonogrammer there to watch it.";

    /// <summary>The fix that lists models already marked able to read images. No new catalogue flag is introduced.</summary>
    public const string ChooseVisionModel = "Choose a vision model";

    /// <summary>Returns the one sentence for <paramref name="state"/>.</summary>
    /// <param name="state">The pane state.</param>
    /// <returns>The sentence.</returns>
    public static string Sentence(BrowserPaneState state) => Row(state).Sentence;

    /// <summary>Returns the one fix action for <paramref name="state"/>.</summary>
    /// <param name="state">The pane state.</param>
    /// <returns>The action.</returns>
    public static string FixAction(BrowserPaneState state) => Row(state).Fix;

    private static (string Sentence, string Fix) Row(BrowserPaneState state) => state switch
    {
        BrowserPaneState.Idle => ("The browser is idle.", "Start the browser"),
        BrowserPaneState.Starting => ("The browser is starting.", "Wait for the page"),
        BrowserPaneState.Live => ("The browser is live.", "Take over"),
        BrowserPaneState.UserControl => ("You control the browser.", "Release control"),
        BrowserPaneState.Stopped => ("The browser is stopped.", "Start the browser"),
        BrowserPaneState.RemoteViewUnavailable => (RemoteViewSentence, "Open the desktop app"),
        BrowserPaneState.BrowserDisabled => ("Browser use is turned off.", "Turn on browser use"),
        BrowserPaneState.BrowserNotInstalled => ("Chromium is not installed.", "Install Chromium"),
        BrowserPaneState.ManagedBrowserNotPinned => ("The managed browser is not pinned.", "Pin the managed browser"),
        BrowserPaneState.BrowserTooOld => ("Chromium is too old.", "Update Chromium"),
        BrowserPaneState.SandboxUnavailable => ("The sandbox is unavailable.", "Enable the sandbox"),
        BrowserPaneState.ProfilePathUnsafe => ("The profile path is unsafe.", "Choose a safe profile path"),
        BrowserPaneState.FontsMissing => ("Fonts are missing.", "Install the fonts"),
        BrowserPaneState.LaunchFailed => ("The browser failed to launch.", "Retry the launch"),
        BrowserPaneState.Crashed => ("The browser crashed.", "Relaunch the browser"),
        BrowserPaneState.UserHasControl => ("You have control of the browser.", "Release control"),
        BrowserPaneState.ControlRevoked => ("Control is revoked until you resume.", "Resume the agent"),
        BrowserPaneState.ModelCannotSeeImages => ("This model cannot see page images.", ChooseVisionModel),
        BrowserPaneState.ImageBudgetExhausted => ("The image budget is exhausted.", "Start a new session"),
        BrowserPaneState.StaleRef => ("That element ref is stale.", "Read the page again"),
        BrowserPaneState.OriginDenied => ("That origin was denied.", "Approve the origin"),
        BrowserPaneState.BlockedByPolicy => ("The navigation was blocked by policy.", "Review the host list"),
        BrowserPaneState.DownloadDenied => ("The download was denied.", "Review download mode"),
        BrowserPaneState.PopupClosed => ("A popup was closed.", "Allow the popup"),
        BrowserPaneState.DialogDismissed => ("A dialog was dismissed.", "Answer the dialog"),
        BrowserPaneState.PageTooLarge => ("The page is too large to read.", "Narrow the read"),
        BrowserPaneState.NavigationTimeout => ("The navigation timed out.", "Retry the navigation"),
        BrowserPaneState.RateLimited => ("Browser actions are rate limited.", "Wait and retry"),
        BrowserPaneState.SessionLimitReached => ("The session limit is reached.", "Close a session"),
        BrowserPaneState.EgressGuardUnavailable => ("The egress guard is unavailable.", "Restart the guard"),
        BrowserPaneState.NoLocalApprover => ("No local approver is present.", "Approve on this desktop"),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown pane state."),
    };
}
