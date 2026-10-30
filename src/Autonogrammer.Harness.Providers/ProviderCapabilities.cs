namespace Autonogrammer.Harness.Providers;

/// <summary>The endpoint routes a provider documents as supported.</summary>
[Flags]
public enum ProviderCapabilities
{
    /// <summary>No route is recorded for the entry.</summary>
    None = 0,

    /// <summary>The /chat/completions route.</summary>
    ChatCompletions = 1,

    /// <summary>The /responses route.</summary>
    Responses = 2,

    /// <summary>The /embeddings route.</summary>
    Embeddings = 4,
}
