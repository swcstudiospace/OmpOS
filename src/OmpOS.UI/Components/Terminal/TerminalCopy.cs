using OmpOS.Presentation.Terminal;

namespace OmpOS.UI.Components.Terminal;

/// <summary>
/// Every sentence, label and accessible name the omp terminal view draws, in one place so the component carries no literal. The
/// unavailable reason is not here: it comes from the transport (or <see cref="TerminalAvailability.WebReason"/> when the head has none).
/// </summary>
public static class TerminalCopy
{
    /// <summary>The accessible name of the terminal region.</summary>
    public const string RegionName = "omp terminal";

    /// <summary>The accessible name of the output screen.</summary>
    public const string ScreenName = "omp terminal output";

    /// <summary>The status text while the stream is being opened.</summary>
    public const string Connecting = "Connecting to the terminal.";

    /// <summary>The status text while the stream is open.</summary>
    public const string Connected = "Connected. The output is read-only; use the box below to steer the session.";

    /// <summary>The status text while the stream is being opened again after it dropped.</summary>
    public const string Reconnecting = "Connection lost. Reconnecting; the output resumes where it stopped.";

    /// <summary>The status text when the terminal finished.</summary>
    public const string Ended = "The terminal has ended.";

    /// <summary>The status text when the connection could not be restored.</summary>
    public const string ConnectionLost = "The connection was lost and could not be restored.";

    /// <summary>The heading shown when the view cannot run in this head.</summary>
    public const string UnavailableHeading = "The omp terminal is not available in this head";

    /// <summary>The reason shown when the screen itself could not be created.</summary>
    public const string ScreenFailed = "The terminal screen could not be loaded in this browser.";

    /// <summary>The reason shown when the transport failed in a way that is not a lost connection.</summary>
    public const string StreamFailed = "The terminal stream failed.";

    /// <summary>The result shown when a steering message could not be sent.</summary>
    public const string SteerFailed = "The message could not be sent.";

    /// <summary>The label of the button that attaches again after the connection was lost.</summary>
    public const string Reconnect = "Reconnect";

    /// <summary>The accessible name of that button.</summary>
    public const string ReconnectName = "Reconnect to the terminal";

    /// <summary>The accessible name of the steering group.</summary>
    public const string SteerName = "Steer the terminal session";

    /// <summary>The accessible name of the steering text box.</summary>
    public const string SteerBoxName = "Steering message";

    /// <summary>The placeholder of the steering text box.</summary>
    public const string SteerPlaceholder = "Message for the session";

    /// <summary>The label of the button that delivers the message into the running turn.</summary>
    public const string SendNow = "Send now";

    /// <summary>The accessible name of the Send now button.</summary>
    public const string SendNowName = "Send now, deliver the message into the running turn";

    /// <summary>The label of the button that queues the message for the next turn.</summary>
    public const string FollowUp = "Follow up";

    /// <summary>The accessible name of the Follow up button.</summary>
    public const string FollowUpName = "Follow up, queue the message for the next turn";

    /// <summary>The note shown when the head gives the view no steering service.</summary>
    public const string SteerUnavailable = "Steering is not available for this terminal.";

    /// <summary>The note under the composer: the terminal accepts steering messages, not keystrokes.</summary>
    public const string SteerHelp = "This view is read-only. Steering messages go to the omp session, not to the shell.";

    /// <summary>The largest steering message the composer accepts, in characters.</summary>
    public const int MaxSteerChars = 500;
}
