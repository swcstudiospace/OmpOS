using System.Collections.Frozen;

namespace OmpOS.Harness.OmpConfig;

/// <summary>Maps <see cref="OmpProviderApi"/> to and from the names omp writes in <c>models.yml</c>.</summary>
internal static class OmpProviderApiNames
{
    private static readonly FrozenDictionary<OmpProviderApi, string> Names = new Dictionary<OmpProviderApi, string>
    {
        [OmpProviderApi.OpenAiCompletions] = "openai-completions",
        [OmpProviderApi.OpenAiResponses] = "openai-responses",
        [OmpProviderApi.AnthropicMessages] = "anthropic-messages",
        [OmpProviderApi.OpenAiCodexResponses] = "openai-codex-responses",
        [OmpProviderApi.AzureOpenAiResponses] = "azure-openai-responses",
        [OmpProviderApi.BedrockConverseStream] = "bedrock-converse-stream",
        [OmpProviderApi.GoogleGenerativeAi] = "google-generative-ai",
        [OmpProviderApi.GoogleGeminiCli] = "google-gemini-cli",
        [OmpProviderApi.GoogleVertex] = "google-vertex",
        [OmpProviderApi.Typesafe] = "typesafe",
        [OmpProviderApi.OpenRouterDecisions] = "openrouter-decisions",
        [OmpProviderApi.OpenAiDecisions] = "openai-decisions",
        [OmpProviderApi.WorkersAiSystemOne] = "workers-ai-systemone",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, OmpProviderApi> Values =
        Names.ToFrozenDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    /// <summary>Returns the omp name of <paramref name="api"/>.</summary>
    /// <param name="api">The protocol.</param>
    /// <returns>The name, or null when <paramref name="api"/> is not a defined value.</returns>
    public static string? ToYaml(OmpProviderApi api) => Names.GetValueOrDefault(api);

    /// <summary>Parses an omp name.</summary>
    /// <param name="name">The name as written in <c>models.yml</c>.</param>
    /// <param name="api">The protocol when the name is known.</param>
    /// <returns>True when the name is one of the thirteen <c>api</c> values omp accepts at provider level.</returns>
    public static bool TryParse(string name, out OmpProviderApi api) => Values.TryGetValue(name, out api);
}
