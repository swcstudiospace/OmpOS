namespace Autonogrammer.UI.Components.Kanban;

/// <summary>
/// Every sentence, label and accessible name the Kanban tab draws that the presentation models do not supply. No component holds a
/// literal; each reads one of these constants or one of the functions below. Status words, column names and card texts come from the
/// models, so the web head says the same things as the desktop head.
/// </summary>
public static class KanbanCopy
{
    /// <summary>The accessible name of the whole board.</summary>
    public const string BoardName = "Kanban board";

    /// <summary>The visible heading of the tab.</summary>
    public const string Heading = "Kanban";

    /// <summary>The accessible name of the scrolling list of columns.</summary>
    public const string ColumnsName = "Kanban columns";

    /// <summary>The one-sentence keyboard help under the heading.</summary>
    public const string KeyboardHelp = "Arrow keys move between cards and columns, Enter opens a session, S steers it.";

    /// <summary>The heading of the loading state.</summary>
    public const string LoadingHeading = "Loading sessions";

    /// <summary>The body of the loading state.</summary>
    public const string LoadingBody = "Reading your sessions.";

    /// <summary>The heading of the empty state.</summary>
    public const string EmptyHeading = "No sessions yet";

    /// <summary>The body of the empty state, with the New agent hint.</summary>
    public const string EmptyBody = "Start a new agent and it appears here as a card.";

    /// <summary>The label of the empty state's button.</summary>
    public const string NewAgent = "New agent";

    /// <summary>The accessible name of the empty state's button.</summary>
    public const string NewAgentName = "New agent, start a session from the Kanban tab";

    /// <summary>The heading of the error state.</summary>
    public const string ErrorHeading = "The sessions could not be read";

    /// <summary>The label of the Retry buttons.</summary>
    public const string Retry = "Retry";

    /// <summary>The accessible name of the Retry button of the error state.</summary>
    public const string RetryName = "Retry reading the sessions";

    /// <summary>The lead of the banner that says the cards are the last good snapshot.</summary>
    public const string BannerLead = "The cards below are the last known sessions.";

    /// <summary>The accessible name of that banner's Retry button.</summary>
    public const string BannerRetryName = "Retry reading the sessions, the cards below are the last known";

    /// <summary>The note shown while no card has Substrate details: they read unavailable, and nothing is invented.</summary>
    public const string SubstrateUnavailable = "Substrate is unavailable, so the task, graph, lessons and lease fields read unavailable.";

    /// <summary>The text of a column that has no card.</summary>
    public const string EmptyColumn = "No tasks";

    /// <summary>The label of the card's steer button.</summary>
    public const string Steer = "Steer";

    /// <summary>The reason written under a disabled Steer button: the session has no steering service.</summary>
    public const string SteerUnavailable = "This session cannot be steered.";

    /// <summary>The reason written under a disabled Steer button: the session has ended.</summary>
    public const string SteerEnded = "This session has ended, so it cannot be steered.";

    /// <summary>The label of the button that delivers the message into the running turn.</summary>
    public const string SendNow = "Send now";

    /// <summary>The accessible name of the Send now button.</summary>
    public const string SendNowName = "Send now, deliver the message into the running turn";

    /// <summary>The label of the button that queues the message for the next turn.</summary>
    public const string QueueNext = "Queue for next turn";

    /// <summary>The accessible name of the Queue button.</summary>
    public const string QueueNextName = "Queue the message for the next turn";

    /// <summary>The accessible name of the steering text box.</summary>
    public const string SteerBoxName = "Steering message";

    /// <summary>The placeholder of the steering text box.</summary>
    public const string SteerPlaceholder = "Message for this session";

    /// <summary>The label of the button that closes the steering box.</summary>
    public const string SteerClose = "Close";

    /// <summary>The accessible name of the closing button.</summary>
    public const string SteerCloseName = "Close the steering box";

    /// <summary>The tag a closed session carries.</summary>
    public const string Closed = "Closed";

    /// <summary>The label of the button that moves a card to Backlog.</summary>
    public const string MoveToBacklog = "Move to Backlog";

    /// <summary>The label of the button that moves a card to Archived.</summary>
    public const string Archive = "Archive";

    /// <summary>Builds the accessible name of a card's steer button.</summary>
    /// <param name="title">The card title.</param>
    /// <returns>The name; it contains the visible word Steer.</returns>
    public static string SteerNameFor(string title) => $"Steer {title}";

    /// <summary>Builds the accessible name of a card's steering box group.</summary>
    /// <param name="title">The card title.</param>
    /// <returns>The name.</returns>
    public static string SteerPanelNameFor(string title) => $"Steer session {title}";

    /// <summary>Builds the accessible name of a card's Move to Backlog button.</summary>
    /// <param name="title">The card title.</param>
    /// <returns>The name; it contains the visible label.</returns>
    public static string MoveToBacklogNameFor(string title) => $"{MoveToBacklog}, {title}";

    /// <summary>Builds the accessible name of a card's Archive button.</summary>
    /// <param name="title">The card title.</param>
    /// <returns>The name; it contains the visible label.</returns>
    public static string ArchiveNameFor(string title) => $"{Archive}, {title}";

    /// <summary>Builds the accessible name of a card's list of Substrate fields.</summary>
    /// <param name="title">The card title.</param>
    /// <returns>The name.</returns>
    public static string SubstrateListNameFor(string title) => $"Substrate details for {title}";
}
