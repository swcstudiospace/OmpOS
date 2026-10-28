using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Providers;

/// <summary>One way to reach a catalogue provider.</summary>
public sealed record ProviderRouteInfo
{
    /// <summary>Gets the route slug, unique inside its provider.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the omp <c>api</c> value the route speaks, for example <c>openai-completions</c>.</summary>
    public required string Api { get; init; }

    /// <summary>Gets the base URL or URL template. A placeholder is written <c>{name}</c>.</summary>
    [JsonPropertyName("baseUrlTemplate")]
    public required string BaseAddressTemplate { get; init; }

    /// <summary>Gets how the key is sent, as words: <c>Bearer</c>, <c>x-api-key</c>, <c>header NAME</c> or <c>none</c>.</summary>
    public required string Auth { get; init; }

    /// <summary>Gets a value indicating whether the route takes a key.</summary>
    public required bool TakesKey { get; init; }

    /// <summary>Gets a value indicating whether omp may discover the models of the route.</summary>
    public required bool DiscoversModels { get; init; }

    /// <summary>Gets a value indicating whether the user has to name a model id.</summary>
    public required bool NeedsUserModels { get; init; }

    /// <summary>Gets the example models the vendor documents.</summary>
    public required IReadOnlyList<string> DocumentedModels { get; init; }
}
