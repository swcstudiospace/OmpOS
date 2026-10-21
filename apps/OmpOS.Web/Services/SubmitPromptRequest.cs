namespace OmpOS.Web.Services;

/// <summary>
/// The body sent to <c>POST /sessions/{id}/prompts</c>.
/// </summary>
internal sealed record SubmitPromptRequest
{
    /// <summary>Gets the user prompt text.</summary>
    public required string Prompt { get; init; }
}
