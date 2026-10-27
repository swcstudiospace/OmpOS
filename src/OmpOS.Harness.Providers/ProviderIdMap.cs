using System.Collections.Frozen;

namespace OmpOS.Harness.Providers;

/// <summary>
/// The explicit table that keeps every id of the typed <see cref="ProviderCatalogue"/> working: each legacy id (<c>ollama-local</c>,
/// <c>deepseek-anthropic</c>, ...) maps to the row and route of <see cref="CatalogueRows"/> that carries the same provider, and to
/// the id omp knows it by (<c>ollama</c>, <c>deepseek</c>, ...; null where omp has no native provider, so a <c>models.yml</c>
/// block is written). A legacy id is a route, an omp id is a provider: several legacy ids can map to one row.
/// </summary>
public static class ProviderIdMap
{
    private static readonly ProviderIdMapping[] Table =
    [
        new("ollama-local", "ollama", "openai", "ollama"),
        new("lm-studio-local", "lm-studio", "openai", "lm-studio"),
        new("llama-cpp-local", "llama-cpp", "openai", "llama.cpp"),
        new("vllm-local", "vllm", "openai", "vllm"),
        new("localai-local", "localai", "openai", null),
        new("jan-local", "jan", "openai", null),
        new("docker-model-runner-local", "docker-model-runner", "openai", null),
        new("ollama-cloud", "ollama-cloud", "openai", "ollama-cloud"),
        new("perplexity", "perplexity", "responses", null),
        new("openrouter-anthropic", "openrouter", "anthropic", "openrouter"),
        new("huggingface-router", "huggingface", "router", "huggingface"),
        new("deepseek-anthropic", "deepseek", "anthropic", "deepseek"),
        new("minimax-m-plan-anthropic", "minimax-m-plan", "anthropic", "minimax-code"),
        new("iflytek-astron-coding-openai", "iflytek-astron-coding", "openai", null),
        new("iflytek-astron-coding-anthropic", "iflytek-astron-coding", "anthropic", null),
    ];

    private static readonly FrozenDictionary<string, ProviderIdMapping> ByLegacyId =
        Table.ToFrozenDictionary(mapping => mapping.LegacyId, StringComparer.Ordinal);

    /// <summary>Gets every mapping, in the order of <see cref="ProviderCatalogue.All"/>.</summary>
    public static IReadOnlyList<ProviderIdMapping> All => Table;

    /// <summary>Returns the mapping of a legacy catalogue id, or null when the id is not a legacy id.</summary>
    /// <param name="legacyId">The id of an entry of <see cref="ProviderCatalogue"/>.</param>
    /// <returns>The mapping, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="legacyId"/> is null.</exception>
    public static ProviderIdMapping? Find(string legacyId)
    {
        ArgumentNullException.ThrowIfNull(legacyId);
        return ByLegacyId.GetValueOrDefault(legacyId);
    }
}
