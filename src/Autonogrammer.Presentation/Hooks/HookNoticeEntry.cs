namespace Autonogrammer.Presentation.Hooks;

/// <summary>Something a hook did that the thread must show, as a head reports it. <see cref="HookNoticeRow.From"/> turns it into a row.</summary>
/// <param name="Type">What the notice tells.</param>
/// <param name="HookName">The name of the hook that caused it, or an empty string when unknown.</param>
/// <param name="ToolCallId">The tool call the notice belongs to, or null when it belongs to the conversation.</param>
/// <param name="Message">The hook's message to the user, the injected context, or an empty string.</param>
/// <param name="Count">How many follow-ups the conversation has had since the user's last message, for a follow-up notice.</param>
/// <param name="Limit">The follow-up cap of the hook, or null when it has none, for a follow-up notice.</param>
public sealed record HookNoticeEntry(HookNoticeType Type, string HookName, string? ToolCallId, string Message, int? Count = null, int? Limit = null);
