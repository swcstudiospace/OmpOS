#pragma warning disable MA0048 // Kind, privacy and the text sanitizer belong with the request.
namespace Autonogrammer.Harness.Notifications;

/// <summary>
/// Why a notification was raised. These are the plan-local kinds: a <c>CoworkToolEvent</c> type was not present under
/// <c>src/Autonogrammer.Cowork</c> when this contract was written, so this assembly does not invent that type.
/// <see cref="NotificationKind.ApprovalNeeded"/> is the kind for an approval-requested tool event and for a permission-queue entry.
/// </summary>
public enum NotificationKind
{
    /// <summary>A run finished.</summary>
    RunFinished = 0,

    /// <summary>An approval is waiting, from an approval-requested tool event or the permission queue.</summary>
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

/// <summary>Whether the operating-system surface may show the summary. Title only is the default.</summary>
public enum NotificationPrivacy
{
    /// <summary>The operating-system surface shows the title only.</summary>
    TitleOnly = 0,

    /// <summary>The operating-system surface may show the summary.</summary>
    ShowContent = 1,
}

/// <summary>One notification to fan out. The summary is kept for the in-app centre even when the operating-system surface is title-only.</summary>
public sealed record NotificationRequest
{
    /// <summary>Initializes a new instance of the <see cref="NotificationRequest"/> class.</summary>
    /// <param name="kind">Why the notification was raised.</param>
    /// <param name="title">The title. Shown on every surface.</param>
    /// <param name="summary">The summary. Hidden on the operating-system surface unless content is shown.</param>
    /// <param name="sessionId">The session the notification belongs to.</param>
    /// <param name="privacy">Whether the operating-system surface may show the summary. Defaults to title only.</param>
    /// <param name="approvalEntryId">The permission-queue entry id when <paramref name="kind"/> is approval needed. The id only, never model text.</param>
    /// <exception cref="ArgumentException">The title or session id is empty.</exception>
    public NotificationRequest(
        NotificationKind kind,
        string title,
        string summary,
        string sessionId,
        NotificationPrivacy privacy = NotificationPrivacy.TitleOnly,
        string? approvalEntryId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        Kind = kind;
        Title = title;
        Summary = summary;
        SessionId = sessionId;
        Privacy = privacy;
        ApprovalEntryId = string.IsNullOrWhiteSpace(approvalEntryId) ? null : approvalEntryId;
    }

    /// <summary>Gets why the notification was raised.</summary>
    public NotificationKind Kind { get; }

    /// <summary>Gets the title.</summary>
    public string Title { get; }

    /// <summary>Gets the summary. It may be empty.</summary>
    public string Summary { get; }

    /// <summary>Gets the session id.</summary>
    public string SessionId { get; }

    /// <summary>Gets whether the operating-system surface may show the summary.</summary>
    public NotificationPrivacy Privacy { get; }

    /// <summary>Gets the permission-queue entry id, when this notification links to one.</summary>
    public string? ApprovalEntryId { get; }
}

/// <summary>Strips control characters and caps each operating-system argument. Notification text is never interpolated into a script.</summary>
internal static class NotificationText
{
    /// <summary>The most characters one operating-system argument may carry.</summary>
    internal const int ArgumentCap = 200;

    /// <summary>Removes control characters and caps the result at <see cref="ArgumentCap"/> characters.</summary>
    /// <param name="value">The text. Null becomes empty.</param>
    /// <returns>The safe argument.</returns>
    internal static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var kept = new char[Math.Min(value.Length, ArgumentCap)];
        var count = 0;
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                continue;
            }

            kept[count++] = character;
            if (count == ArgumentCap)
            {
                break;
            }
        }

        return new string(kept, 0, count);
    }
}
