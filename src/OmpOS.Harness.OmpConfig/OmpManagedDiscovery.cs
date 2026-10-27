using System.Collections.Frozen;

namespace OmpOS.Harness.OmpConfig;

/// <summary>
/// The <c>discovery</c> member of a managed provider: omp asks the endpoint for its model list instead of reading models from
/// <c>models.yml</c>. Only the <c>type</c> is written; omp's <c>injectV1</c> and <c>timeoutMs</c> keep their defaults.
/// </summary>
/// <param name="Type">One of omp's discovery types, for example <see cref="OpenAiModelsList"/>.</param>
public sealed record OmpManagedDiscovery(string Type)
{
    /// <summary>The <c>openai-models-list</c> type: omp fetches <c>{baseUrl}/models</c> with the key.</summary>
    public const string OpenAiModelsList = "openai-models-list";

    private static readonly FrozenSet<string> KnownTypes = new[]
    {
        "ollama", "llama.cpp", "lm-studio", OpenAiModelsList, "proxy", "litellm", "apple-foundation-models",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Tells whether <paramref name="type"/> is a discovery type omp's schema accepts.</summary>
    /// <param name="type">The type name.</param>
    /// <returns>True when omp accepts it.</returns>
    public static bool IsKnownType(string? type) => type is not null && KnownTypes.Contains(type);
}
