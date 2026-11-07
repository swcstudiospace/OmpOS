#pragma warning disable MA0048 // The kind and the list row belong with the item.
namespace Autonogrammer.Presentation.Notifications;

/// <summary>
/// The kind shown in the in-app centre. These names are local to Presentation so this assembly does not reference
/// Harness.Notifications.
/// </summary>
public enum NotificationCentreKind
{
    /// <summary>A run finished.</summary>
    RunFinished = 0,

    /// <summary>An approval is waiting.</summary>
    ApprovalNeeded = 1,

    /// <summary>A scheduled task finished.</summary>
    ScheduledTaskFinished = 2,

    /// <summary>A run or scheduled task failed.</summary>
    Failed = 3,

    /// <summary>A scheduled task was missed.</summary>
    Missed = 4,

    /// <summary>A scheduled task is waiting.</summary>
    Waiting = 5,
}

/// <summary>One in-app notification. The approval link is the queue entry id and never includes the title or the summary.</summary>
/// <param name="Id">The item id.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Title">The title.</param>
/// <param name="Summary">The summary, kept even when the row is title only.</param>
/// <param name="SessionId">The session id.</param>
/// <param name="TitleOnly">True when the summary stays hidden until Show content is on.</param>
/// <param name="ApprovalQueueEntryId">The permission-queue entry id, or null when this item does not link to the queue.</param>
public sealed record NotificationItemModel(
    string Id,
    NotificationCentreKind Kind,
    string Title,
    string? Summary,
    string SessionId,
    bool TitleOnly,
    string? ApprovalQueueEntryId)
{
    /// <summary>Gets the queue entry id an approval-needed item links to. The value is the id alone.</summary>
    public string? ApprovalLinkTarget =>
        Kind == NotificationCentreKind.ApprovalNeeded && !string.IsNullOrWhiteSpace(ApprovalQueueEntryId)
            ? ApprovalQueueEntryId
            : null;
}

/// <summary>One row of the centre list, with the summary omitted until Show content is on.</summary>
/// <param name="Kind">The kind label.</param>
/// <param name="Title">The title.</param>
/// <param name="SessionId">The session id.</param>
/// <param name="Privacy">Whether the item is title only.</param>
/// <param name="VisibleSummary">The summary, or null while Show content is off.</param>
/// <param name="ApprovalLinkTarget">The queue entry id, or null.</param>
/// <param name="AccessibleName">The accessible name. It does not include the summary while the row is title only.</param>
public sealed record NotificationListEntry(
    string Kind,
    string Title,
    string SessionId,
    string Privacy,
    string? VisibleSummary,
    string? ApprovalLinkTarget,
    string AccessibleName)
{
    /// <summary>Gets whether the row shows a summary.</summary>
    public bool HasVisibleSummary => !string.IsNullOrEmpty(VisibleSummary);

    /// <summary>Gets whether the row links to the approval queue.</summary>
    public bool HasApprovalLink => ApprovalLinkTarget is not null;

    /// <summary>Gets the accessible name of the approval link. It is the entry id and does not include the title or the summary.</summary>
    public string? ApprovalLinkName => ApprovalLinkTarget;
}
