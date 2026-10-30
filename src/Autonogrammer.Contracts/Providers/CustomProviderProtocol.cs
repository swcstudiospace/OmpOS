using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Providers;

/// <summary>The wire protocol of a custom endpoint.</summary>
public enum CustomProviderProtocol
{
    /// <summary>OpenAI chat completions.</summary>
    [JsonStringEnumMemberName("openai")]
    OpenAi,

    /// <summary>Anthropic messages.</summary>
    [JsonStringEnumMemberName("anthropic")]
    Anthropic,
}
