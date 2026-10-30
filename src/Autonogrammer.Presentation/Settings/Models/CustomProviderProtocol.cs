namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>The request protocol a custom provider speaks.</summary>
public enum CustomProviderProtocol
{
    /// <summary>An OpenAI-compatible endpoint; its base address usually ends in <c>/v1</c>.</summary>
    OpenAiCompatible = 0,

    /// <summary>An Anthropic Messages endpoint; its base address is the root, without <c>/v1</c>.</summary>
    AnthropicCompatible = 1,
}
