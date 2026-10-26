namespace OmpOS.Contracts.Streaming;

/// <summary>
/// The user's own prompt, recorded in the event log when the prompt is sent (kind "user_message"). Every head folds it into one user
/// turn ahead of the assistant output it prompted. A reader that predates this kind keeps the raw event as <see cref="UnknownPayload"/>.
/// </summary>
public sealed record UserMessagePayload : StreamEventPayload
{
    /// <summary>Gets the verbatim prompt text.</summary>
    public required string Text { get; init; }
}
