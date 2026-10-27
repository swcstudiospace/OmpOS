using System.Collections.Frozen;

namespace OmpOS.Harness.Providers;

/// <summary>The <c>api</c> values omp's <c>models.yml</c> accepts for a route, as the omp 18.12.6 models documentation lists them.</summary>
public static class CatalogueApiNames
{
    /// <summary>The ten omp <c>api</c> values a catalogue route may use.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        "openai-completions",
        "openai-responses",
        "openai-codex-responses",
        "azure-openai-responses",
        "anthropic-messages",
        "bedrock-converse-stream",
        "google-generative-ai",
        "google-gemini-cli",
        "google-vertex",
        "typesafe",
    ];

    private static readonly FrozenSet<string> Known = All.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Tells whether <paramref name="api"/> is one of the ten values.</summary>
    /// <param name="api">The value to check.</param>
    /// <returns>True when omp accepts the value in a route.</returns>
    public static bool IsKnown(string? api) => api is not null && Known.Contains(api);
}
