#pragma warning disable MA0048 // The plan owns this file for the state enum and its sentences.
namespace Autonogrammer.Contracts.Cowork.Browser;

/// <summary>
/// What the pane is showing, and the refusal names the harness returns to the model. Web and Android stay on <see cref="RemoteViewUnavailable"/> because no relay feed is built.
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

    /// <summary>Chromium is older than the pin.</summary>
    BrowserTooOld,

    /// <summary>The current model cannot read images.</summary>
    ModelCannotSeeImages,

    /// <summary>An element ref no longer matches the page.</summary>
    StaleRef,

    /// <summary>The download was denied.</summary>
    DownloadDenied,
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
        BrowserPaneState.BrowserTooOld => ("Chromium is too old.", "Update Chromium"),
        BrowserPaneState.ModelCannotSeeImages => ("This model cannot see page images.", ChooseVisionModel),
        BrowserPaneState.StaleRef => ("That element ref is stale.", "Read the page again"),
        BrowserPaneState.DownloadDenied => ("The download was denied.", "Review download mode"),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown pane state."),
    };
}
