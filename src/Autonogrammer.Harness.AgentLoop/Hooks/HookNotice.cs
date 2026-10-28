namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>Something a hook did that the thread must show to the user. It is raised by a session while the conversation runs.</summary>
public sealed record HookNotice
{
    /// <summary>Gets what the notice tells.</summary>
    public required HookNoticeKind Kind { get; init; }

    /// <summary>Gets the session the notice belongs to.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets when the notice was raised.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>Gets the event whose hooks caused the notice.</summary>
    public required string EventName { get; init; }

    /// <summary>Gets the tool call the notice belongs to, or null when it belongs to the conversation.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Gets the identifier of the hook that caused the notice, or null.</summary>
    public string? HookId { get; init; }

    /// <summary>Gets the text for the user: the hook's <c>user_message</c>, the injected context, or the explanation of the continuation.</summary>
    public required string Message { get; init; }

    /// <summary>Gets how many follow-ups the conversation has had since the user's last message, for <see cref="HookNoticeKind.FollowUp"/>.</summary>
    public int? Count { get; init; }

    /// <summary>Gets the follow-up limit of the hook, or null when it has none, for <see cref="HookNoticeKind.FollowUp"/>.</summary>
    public int? Limit { get; init; }
}
