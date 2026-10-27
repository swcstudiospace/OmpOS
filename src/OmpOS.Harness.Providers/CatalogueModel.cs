namespace OmpOS.Harness.Providers;

/// <summary>A model id a vendor documents for a route. Models are listed only where the route has no documented models list.</summary>
/// <param name="Id">The model id the endpoint expects.</param>
/// <param name="Name">The display name, or null to show the id.</param>
/// <param name="Reasoning">True when the vendor documents the model as a reasoning model.</param>
/// <param name="ContextWindow">The documented context window in tokens, or null.</param>
/// <param name="MaxTokens">The documented output limit in tokens, or null.</param>
public sealed record CatalogueModel(
    string Id,
    string? Name = null,
    bool Reasoning = false,
    int? ContextWindow = null,
    int? MaxTokens = null);
