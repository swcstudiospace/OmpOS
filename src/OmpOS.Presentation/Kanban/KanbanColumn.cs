using Status = OmpOS.Presentation.Kanban.RuntimeStatus;

namespace OmpOS.Presentation.Kanban;

/// <summary>
/// One column of the board. A runtime column shows the cards whose <see cref="RuntimeStatus"/> it names and never accepts a drop; the
/// Backlog and Archived columns are the only ones that accept one. Column names are the 39-CONTEXT defaults until the owner confirms others.
/// </summary>
public sealed record KanbanColumn
{
    /// <summary>The key of the Backlog column.</summary>
    public const string BacklogKey = "backlog";

    /// <summary>The key of the Archived column.</summary>
    public const string ArchivedKey = "archived";

    /// <summary>Gets the stable column key.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the column title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the runtime status the column shows, or null for Backlog and Archived.</summary>
    public RuntimeStatus? RuntimeStatus { get; init; }

    /// <summary>Gets the user-settable status the column stands for, or null for a runtime column.</summary>
    public UserSettableStatus? UserStatus { get; init; }

    /// <summary>Gets the cards of the column, most recent activity first.</summary>
    public required IReadOnlyList<KanbanCard> Cards { get; init; }

    /// <summary>Gets whether a dragged card may be dropped here: true only for Backlog and Archived.</summary>
    public bool AcceptsDrop => UserStatus is not null;

    /// <summary>Gets the number of cards.</summary>
    public int Count => Cards.Count;

    /// <summary>Gets the accessible name: the title and the number of cards.</summary>
    public string AccessibleName => Count == 1 ? $"{Title}, 1 task" : $"{Title}, {Count} tasks";

    /// <summary>Returns the key of the column that shows a runtime status.</summary>
    /// <param name="status">The runtime status.</param>
    /// <returns>The column key, the status name in lower case.</returns>
    public static string KeyOf(RuntimeStatus status) => status switch
    {
        Status.Queued => "queued",
        Status.Running => "running",
        Status.AwaitingInput => "awaiting-input",
        Status.NeedsAttention => "needs-attention",
        Status.Paused => "paused",
        Status.Completed => "completed",
        Status.Failed => "failed",
        Status.Unreachable => "unreachable",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The runtime status has no column."),
    };

    /// <summary>Returns the key of the column of a user-settable status.</summary>
    /// <param name="status">The user-settable status.</param>
    /// <returns>The column key.</returns>
    public static string KeyOf(UserSettableStatus status) => status switch
    {
        UserSettableStatus.Backlog => BacklogKey,
        UserSettableStatus.Archived => ArchivedKey,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The status has no column."),
    };

    /// <summary>Returns the words of a runtime status, which are also its column title.</summary>
    /// <param name="status">The runtime status.</param>
    /// <returns>The title, for example <c>Needs attention</c>.</returns>
    public static string TitleOf(RuntimeStatus status) => status switch
    {
        Status.Queued => "Queued",
        Status.Running => "Running",
        Status.AwaitingInput => "Awaiting input",
        Status.NeedsAttention => "Needs attention",
        Status.Paused => "Paused",
        Status.Completed => "Completed",
        Status.Failed => "Failed",
        Status.Unreachable => "Unreachable",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The runtime status has no title."),
    };

    /// <summary>Returns the title of a user-settable column.</summary>
    /// <param name="status">The user-settable status.</param>
    /// <returns>The title.</returns>
    public static string TitleOf(UserSettableStatus status) => status switch
    {
        UserSettableStatus.Backlog => "Backlog",
        UserSettableStatus.Archived => "Archived",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The status has no title."),
    };
}
