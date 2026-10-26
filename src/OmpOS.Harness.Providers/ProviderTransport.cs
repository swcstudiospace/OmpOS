namespace OmpOS.Harness.Providers;

/// <summary>The request protocol an endpoint speaks.</summary>
public enum ProviderTransport
{
    /// <summary>An OpenAI-compatible base URL, usually ending in /v1.</summary>
    OpenAiCompatible = 0,

    /// <summary>An Anthropic Messages API (/v1/messages). Its base URL is the root with no /v1 suffix.</summary>
    AnthropicCompatible,
}
