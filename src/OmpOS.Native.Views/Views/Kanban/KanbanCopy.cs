namespace OmpOS.Native.Views.Kanban;

/// <summary>
/// Every sentence, label and accessible name the Kanban tab draws that the presentation models do not supply. No view holds a literal;
/// each reads one of these constants or one of the functions below. Status words, column names and card texts come from the models.
/// </summary>
internal static class KanbanCopy
{
    /// <summary>The accessible name of the whole board.</summary>
    public const string BoardName = "Kanban board";

    /// <summary>The heading of the loading state.</summary>
    public const string LoadingHeading = "Loading sessions";

    /// <summary>The body of the loading state.</summary>
    public const string LoadingBody = "Reading your sessions.";

    /// <summary>The accessible name of the skeleton cards.</summary>
    public const string SkeletonName = "Session cards are loading";

    /// <summary>The heading of the empty state.</summary>
    public const string EmptyHeading = "No sessions yet";

    /// <summary>The body of the empty state.</summary>
    public const string EmptyBody = "Start a session and it appears here as a card.";

    /// <summary>The label of the empty state's button.</summary>
    public const string StartSession = "Start a session";

    /// <summary>The accessible name of the empty state's button.</summary>
    public const string StartSessionName = "Start a session from the Kanban tab";

    /// <summary>The heading of the error state.</summary>
    public const string ErrorHeading = "The sessions could not be read";

    /// <summary>The label of the Retry buttons.</summary>
    public const string Retry = "Retry";

    /// <summary>The accessible name of the Retry button of the error state.</summary>
    public const string RetryName = "Retry reading the sessions";

    /// <summary>The accessible name of the Retry button of the banner above the columns.</summary>
    public const string BannerRetryName = "Retry reading the sessions, the cards below are the last known";

    /// <summary>The lead of the banner that says the cards are the last good snapshot.</summary>
    public const string BannerLead = "The cards below are the last known sessions.";

    /// <summary>The accessible name of the banner.</summary>
    public const string BannerName = "Session list warning";

    /// <summary>The text of a column that has no card.</summary>
    public const string EmptyColumn = "No tasks";

    /// <summary>The hint under the title of a column that accepts a dropped card.</summary>
    public const string DropHint = "Drop a card here";

    /// <summary>The label of the card's steer button.</summary>
    public const string Steer = "Steer";

    /// <summary>The tooltip of the steer button.</summary>
    public const string SteerTip = "Steer this session (S)";

    /// <summary>The label of the button that delivers the message into the running turn.</summary>
    public const string SendNow = "Send now";

    /// <summary>The accessible name of the Send now button.</summary>
    public const string SendNowName = "Send now, deliver the message into the running turn";

    /// <summary>The label of the button that queues the message for the end of the turn.</summary>
    public const string QueueNext = "Queue for next turn";

    /// <summary>The accessible name of the Queue for next turn button.</summary>
    public const string QueueNextName = "Queue the message for the next turn";

    /// <summary>The accessible name of the steering message box.</summary>
    public const string SteerBoxName = "Steering message";

    /// <summary>The placeholder of the steering message box.</summary>
    public const string SteerPlaceholder = "Message for this session";

    /// <summary>The accessible name of the steering result line.</summary>
    public const string SteerResultName = "Steering result";

    /// <summary>The accessible name of the live region that announces card changes.</summary>
    public const string LiveRegionName = "Kanban updates";

    /// <summary>The words shown on a card whose session the operator closed.</summary>
    public const string Closed = "Closed";

    /// <summary>The tooltip of a card.</summary>
    public const string CardTip = "Enter opens the session, S steers it, the arrow keys move between cards";

    /// <summary>The accessible name of the columns list.</summary>
    public const string ColumnsName = "Kanban columns";

    /// <summary>Returns the accessible name of the steer button of one card.</summary>
    /// <param name="title">The card's title.</param>
    /// <returns>The name, for example <c>Steer Build the board</c>.</returns>
    internal static string SteerNameFor(string title) => $"Steer {title}";

    /// <summary>Returns the accessible name of the steering panel of one card.</summary>
    /// <param name="title">The card's title.</param>
    /// <returns>The name.</returns>
    internal static string SteerPanelNameFor(string title) => $"Steer session {title}";
}
