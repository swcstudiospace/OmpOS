using System.Text.Json.Serialization;

namespace OmpOS.Harness.Providers;

/// <summary>
/// One way to reach a provider: an omp <c>api</c> value, a base URL (a template when it holds <c>{variable}</c> placeholders), how the
/// key is sent, and the documented models. A row has one default route (the first) and may have more, for example an OpenAI route
/// and an Anthropic route on the same vendor.
/// </summary>
/// <param name="Id">The route slug, unique inside its row.</param>
/// <param name="Api">The omp <c>models.yml</c> <c>api</c> value the route speaks, for example <c>openai-completions</c>.</param>
/// <param name="BaseAddressTemplate">The base URL or URL template. It is https, except that a local runtime may use http on a loopback host.</param>
/// <param name="Auth">How the key is sent.</param>
/// <param name="AuthHeader">The header that carries the key when <paramref name="Auth"/> is <see cref="CatalogueAuthStyle.CustomHeader"/>; null otherwise.</param>
/// <param name="Discovery">True when the vendor documents a models list under the base URL, so omp may discover the models.</param>
/// <param name="ModelsEndpoint">The documented models-list route as text, or null.</param>
/// <param name="Models">The documented example models, used when <paramref name="Discovery"/> is false.</param>
public sealed record CatalogueRoute(
    string Id,
    string Api,
    [property: JsonPropertyName("baseUrl")] string BaseAddressTemplate,
    CatalogueAuthStyle Auth,
    string? AuthHeader,
    bool Discovery,
    string? ModelsEndpoint,
    IReadOnlyList<CatalogueModel> Models)
{
    /// <summary>
    /// Gets a value indicating whether the user has to name a model id: the vendor documents no models list the research could
    /// confirm and no example model, so the form asks for at least one id before a <c>models.yml</c> block is written.
    /// </summary>
    public bool NeedsUserModels => !Discovery && Models.Count == 0;
}
