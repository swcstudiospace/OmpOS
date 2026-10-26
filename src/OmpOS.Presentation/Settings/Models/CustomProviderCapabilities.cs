namespace OmpOS.Presentation.Settings.Models;

/// <summary>The routes a custom provider is recorded as supporting.</summary>
[Flags]
public enum CustomProviderCapabilities
{
    /// <summary>No route recorded.</summary>
    None = 0,

    /// <summary>Chat completions.</summary>
    Chat = 1 << 0,

    /// <summary>The Responses route.</summary>
    Responses = 1 << 1,

    /// <summary>Embeddings.</summary>
    Embeddings = 1 << 2,
}
