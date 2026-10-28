using OmpOS.Contracts.Hooks;

namespace OmpOS.Presentation.Hooks;

/// <summary>
/// A row a hook adds to the thread: <c>denied by hook &lt;name&gt;: &lt;message&gt;</c> under the tool call it denied, a collapsible <c>Hook context</c>
/// row that shows what a hook injected, or <c>Hook continued the conversation (n of loop_limit)</c>. The row carries plain text only and never
/// the model-facing message of a denial.
/// </summary>
public sealed class HookNoticeRow
{
    private HookNoticeRow(HookNoticeType type, string text, string detail, string? toolCallId, string hookName)
    {
        Type = type;
        Text = text;
        Detail = detail;
        ToolCallId = toolCallId;
        HookName = hookName;
    }

    /// <summary>Gets what the notice tells.</summary>
    public HookNoticeType Type { get; }

    /// <summary>Gets the line that is always visible.</summary>
    public string Text { get; }

    /// <summary>Gets the text that is revealed when the row is expanded, or an empty string when the row cannot be expanded.</summary>
    public string Detail { get; }

    /// <summary>Gets a value indicating whether the row can be expanded to show <see cref="Detail"/>. Only a context row can.</summary>
    public bool IsCollapsible => Detail.Length > 0;

    /// <summary>Gets the tool call the row belongs to, or null when it belongs to the conversation.</summary>
    public string? ToolCallId { get; }

    /// <summary>Gets a value indicating whether the row is shown under a tool call card.</summary>
    public bool IsAttachedToToolCall => ToolCallId is not null;

    /// <summary>Gets the name of the hook that caused the notice, or an empty string.</summary>
    public string HookName { get; }

    /// <summary>Gets a value indicating whether the row offers the review action: the hook needs approval.</summary>
    public bool OffersReview => Type == HookNoticeType.ConsentRequired;

    /// <summary>Gets the label of the review action.</summary>
    public string ReviewLabel { get; } = HooksCopy.ReviewLabel;

    /// <summary>Gets the accessible name of the row.</summary>
    public string AutomationName => IsCollapsible ? HooksCopy.ContextSummary + ": " + Text : Text;

    /// <summary>Builds the row of a notice.</summary>
    /// <param name="entry">The notice.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public static HookNoticeRow From(HookNoticeEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var name = entry.HookName;
        var message = HookLogRedaction.Redact(entry.Message) ?? string.Empty;
        return entry.Type switch
        {
            HookNoticeType.Denied => new HookNoticeRow(entry.Type, HooksCopy.DeniedNotice(name, message), string.Empty, entry.ToolCallId, entry.HookName),
            HookNoticeType.ConsentRequired => new HookNoticeRow(entry.Type, HooksCopy.ConsentNotice(name), string.Empty, entry.ToolCallId, entry.HookName),
            HookNoticeType.Context => new HookNoticeRow(entry.Type, HooksCopy.ContextSummary, message, entry.ToolCallId, entry.HookName),
            _ => new HookNoticeRow(entry.Type, HooksCopy.FollowUpNotice(entry.Count ?? 1, entry.Limit), string.Empty, entry.ToolCallId, entry.HookName),
        };
    }
}
