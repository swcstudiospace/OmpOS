namespace OmpOS.EngineHost;

/// <summary>
/// Request body for <c>POST /sessions/{id}/prompts</c>.
/// </summary>
internal sealed record PromptBody
{
    /// <summary>Gets the prompt text.</summary>
    public required string Prompt { get; init; }
}
