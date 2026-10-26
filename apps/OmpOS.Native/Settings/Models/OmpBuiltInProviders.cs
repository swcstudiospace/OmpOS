using System.Collections.Frozen;

namespace OmpOS.Native.Settings.Models;

/// <summary>
/// The API-key providers omp knows by itself that the Models page lists even before a key exists, and the local engines omp discovers.
/// Every API-key row was verified in a sandbox on 2026-10-26 against omp 18.12.6: with a fake key in the provider's own environment
/// variable (the variable named in <c>omp://providers.md</c>), <c>omp models --json --no-extensions</c> listed models of that provider
/// id, and with no variable it listed none. A provider that failed the check is not in the table: <c>siliconflow</c>, <c>perplexity</c>
/// and <c>cohere</c> listed no row. The key page is the address the provider issues keys at, kept only where it could be read on the
/// date; a page that answered 403, 404 or 429 to a plain request has no address here. The table is a convenience: omp's own listing
/// adds every other provider it holds a credential for.
/// </summary>
internal static class OmpBuiltInProviders
{
    private static readonly DateOnly ReadDate = new(2026, 10, 26);

    private static readonly OmpBuiltInProvider[] Providers =
    [
        Row("openai", "OpenAI", null),
        Row("anthropic", "Anthropic", "https://platform.claude.com/settings/keys"),
        Row("google", "Google Gemini", "https://aistudio.google.com/apikey"),
        Row("groq", "Groq", "https://console.groq.com/keys"),
        Row("openrouter", "OpenRouter", "https://openrouter.ai/keys"),
        Row("mistral", "Mistral", "https://console.mistral.ai/api-keys"),
        Row("xai", "xAI", null),
        Row("cerebras", "Cerebras", "https://cloud.cerebras.ai/"),
        Row("deepseek", "DeepSeek", null),
        Row("fireworks", "Fireworks AI", null),
        Row("together", "Together AI", "https://api.together.ai/settings/api-keys"),
        Row("nvidia", "NVIDIA", "https://build.nvidia.com/"),
        Row("deepinfra", "DeepInfra", "https://deepinfra.com/dash/api_keys"),
        Row("huggingface", "Hugging Face", "https://huggingface.co/settings/tokens"),
        Row("moonshot", "Moonshot AI", "https://platform.kimi.ai/console/api-keys"),
        Row("stepfun", "StepFun", "https://platform.stepfun.com/"),
        Row("novita", "Novita AI", "https://novita.ai/settings/key-management"),
        Row("venice", "Venice", "https://venice.ai/settings/api"),
        Row("inception", "Inception", null),
        Row("baseten", "Baseten", "https://app.baseten.co/settings/api_keys"),
        Row("nanogpt", "NanoGPT", "https://nano-gpt.com/api"),
        Row("vercel-ai-gateway", "Vercel AI Gateway", "https://vercel.com/ai-gateway"),
        Row("zai", "Z.AI", "https://z.ai/manage-apikey/apikey-list"),
        Row("minimax", "MiniMax", "https://platform.minimax.io/docs/guides/models-intro"),
    ];

    private static readonly OmpLocalEngine[] Engines =
    [
        new OmpLocalEngine("ollama", "Ollama", new Uri("http://127.0.0.1:11434")),
        new OmpLocalEngine("lm-studio", "LM Studio", new Uri("http://127.0.0.1:1234/v1")),
        new OmpLocalEngine("llama.cpp", "llama.cpp", new Uri("http://127.0.0.1:8080")),
    ];

    private static readonly FrozenDictionary<string, OmpBuiltInProvider> ById =
        Providers.ToFrozenDictionary(static provider => provider.Id, StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, OmpLocalEngine> LocalById =
        Engines.ToFrozenDictionary(static engine => engine.Id, StringComparer.Ordinal);

    /// <summary>Gets the verified API-key providers, in the order the page lists them.</summary>
    public static IReadOnlyList<OmpBuiltInProvider> All => Providers;

    /// <summary>Gets the local engines omp discovers, with the default address of each as <c>omp://providers.md</c> documents it.</summary>
    public static IReadOnlyList<OmpLocalEngine> LocalEngines => Engines;

    /// <summary>Returns the API-key provider with an id, or null.</summary>
    /// <param name="providerId">The provider id.</param>
    /// <returns>The row, or null when the id is not in the table.</returns>
    public static OmpBuiltInProvider? Find(string providerId)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        return ById.GetValueOrDefault(providerId);
    }

    /// <summary>Returns the local engine with an id, or null.</summary>
    /// <param name="providerId">The provider id.</param>
    /// <returns>The engine, or null when the id is not one omp discovers.</returns>
    public static OmpLocalEngine? FindLocalEngine(string providerId)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        return LocalById.GetValueOrDefault(providerId);
    }

    private static OmpBuiltInProvider Row(string id, string displayName, string? keyPage) =>
        new(id, displayName, keyPage is null ? null : new Uri(keyPage), keyPage is null ? null : ReadDate);
}
