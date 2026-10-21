namespace OmpOS.Api.Endpoints;

/// <summary>
/// Request body for <c>POST /sessions/{id}/prompts</c>.
/// </summary>
internal sealed record PromptRequest
{
    /// <summary>Gets the prompt text. Required, non-empty, within the configured length limit.</summary>
    public string? Prompt { get; init; }
}
