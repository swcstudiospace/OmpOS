namespace OmpOS.Presentation.Settings.Models;

/// <summary>One way to reach a listed provider: a protocol, a base URL (or template) and how the key is sent.</summary>
/// <param name="Id">The route id, unique inside its listing.</param>
/// <param name="ApiName">The omp <c>api</c> value of the route, for example <c>openai-completions</c>.</param>
/// <param name="BaseAddressTemplate">The base URL; it holds <c>{name}</c> placeholders when the listing has URL variables.</param>
/// <param name="AuthText">How the key is sent, as words, for example <c>Bearer</c> or <c>header X-API-Key</c>.</param>
/// <param name="TakesKey">True when the route needs a key; false for a keyless local runtime.</param>
/// <param name="DiscoversModels">True when the vendor documents a models list omp can read.</param>
/// <param name="NeedsUserModels">True when the user has to name at least one model id before the provider can be added.</param>
/// <param name="DocumentedModels">The model ids the vendor documents for the route.</param>
public sealed record ProviderListingRoute(
    string Id,
    string ApiName,
    string BaseAddressTemplate,
    string AuthText,
    bool TakesKey,
    bool DiscoversModels,
    bool NeedsUserModels,
    IReadOnlyList<string> DocumentedModels)
{
    /// <summary>Gets the text of the route in a list: the protocol and the base URL.</summary>
    public string Label => $"{ApiName}: {BaseAddressTemplate}";
}
