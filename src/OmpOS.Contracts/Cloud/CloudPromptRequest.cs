using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Cloud;

/// <summary>The body of <c>POST /cloud/agents/{id}/prompts</c>.</summary>
public sealed record CloudPromptRequest
{
    /// <summary>Gets the prompt text. Required, not blank, within the configured prompt length.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Prompt { get; init; }
}
