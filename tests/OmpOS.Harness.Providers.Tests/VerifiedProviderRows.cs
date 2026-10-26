namespace OmpOS.Harness.Providers.Tests;

/// <summary>
/// Provider entries seeded from VERIFIED research rows only. Each property cites its row as file:line in
/// docs/plans/vision-research/ and names the route it reproduces when the row lists more than one route. Only facts
/// that the row states are copied; no key value is stored or used.
/// </summary>
internal static class VerifiedProviderRows
{
    private static readonly DateOnly ResearchDate = new(2026, 10, 22);

    // providers-local-us.md:15 Ollama, status verified. OpenAI-compatible route on the local base http://localhost:11434/v1/.
    // The row says the key is required by the client but ignored locally, so the entry is ApiKey. The Anthropic route
    // of the same row (base http://localhost:11434) is not seeded.
    public static ProviderEntry OllamaLocalOpenAi => new(
        "ollama-local",
        ProviderKind.Local,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.ApiKey,
        new Uri("http://localhost:11434/v1/"),
        ProviderCapabilities.ChatCompletions | ProviderCapabilities.Responses,
        new Uri("https://docs.ollama.com/api/openai-compatibility"),
        ResearchDate);

    // providers-local-us.md:15 Ollama, status verified. OpenAI-compatible route on the cloud base https://ollama.com/v1; Bearer key.
    public static ProviderEntry OllamaCloudOpenAi => new(
        "ollama-cloud",
        ProviderKind.ApiKey,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.BearerToken,
        new Uri("https://ollama.com/v1"),
        ProviderCapabilities.ChatCompletions | ProviderCapabilities.Responses,
        new Uri("https://docs.ollama.com/api/openai-compatibility"),
        ResearchDate);

    // providers-local-us.md:17 llama.cpp server, status verified. OpenAI-compatible route on the default base
    // http://127.0.0.1:9931; --api-key flag, header form not stated, so ApiKey.
    public static ProviderEntry LlamaCppLocalOpenAi => new(
        "llama-cpp-local",
        ProviderKind.Local,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.ApiKey,
        new Uri("http://127.0.0.1:9931"),
        ProviderCapabilities.ChatCompletions | ProviderCapabilities.Responses | ProviderCapabilities.Embeddings,
        new Uri("https://raw.githubusercontent.com/ggml-org/llama.cpp/master/tools/server/README.md"),
        ResearchDate);

    // providers-local-us.md:36 Perplexity, status verified. Base https://api.perplexity.ai/v1; Responses format only
    // (chat completions not confirmed); API key, header not stated, so ApiKey.
    public static ProviderEntry Perplexity => new(
        "perplexity",
        ProviderKind.ApiKey,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.ApiKey,
        new Uri("https://api.perplexity.ai/v1"),
        ProviderCapabilities.Responses,
        new Uri("https://docs.perplexity.ai/docs/agent-api/openai-compatibility"),
        ResearchDate);

    // providers-local-us.md:31 OpenRouter, status verified. Anthropic route for Claude Code, base https://openrouter.ai/api;
    // Bearer (ANTHROPIC_AUTH_TOKEN). The Anthropic route has no OpenAI route flag, so Capabilities is None. The row's
    // OpenAI-compatible base https://openrouter.ai/api/v1 is not seeded.
    public static ProviderEntry OpenRouterAnthropic => new(
        "openrouter-anthropic",
        ProviderKind.ApiKey,
        ProviderTransport.AnthropicCompatible,
        ProviderAuthType.BearerToken,
        new Uri("https://openrouter.ai/api"),
        ProviderCapabilities.None,
        new Uri("https://openrouter.ai/docs/cookbook/coding-agents/claude-code-integration"),
        ResearchDate);

    // providers-eu-cn.md:19 Hugging Face Inference Providers (EU-7), status VERIFIED. OpenAI-compatible base
    // https://router.huggingface.co/v1; HF token, Bearer (V). The row marks OpenAI-compatible V and names no route under
    // it, so no route flag is recorded and Capabilities is None.
    public static ProviderEntry HuggingFaceRouter => new(
        "huggingface-router",
        ProviderKind.ApiKey,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.BearerToken,
        new Uri("https://router.huggingface.co/v1"),
        ProviderCapabilities.None,
        new Uri("https://huggingface.co/docs/inference-providers/index"),
        ResearchDate);

    // providers-eu-cn.md:22 DeepSeek (CN-1), status VERIFIED. Anthropic route, base https://api.deepseek.com/anthropic (V);
    // API key, Bearer (V). The Anthropic route has no OpenAI route flag, so Capabilities is None. The row's OpenAI route
    // https://api.deepseek.com is not seeded.
    public static ProviderEntry DeepSeekAnthropic => new(
        "deepseek-anthropic",
        ProviderKind.ApiKey,
        ProviderTransport.AnthropicCompatible,
        ProviderAuthType.BearerToken,
        new Uri("https://api.deepseek.com/anthropic"),
        ProviderCapabilities.None,
        new Uri("https://api-docs.deepseek.com/guides/anthropic_api/"),
        ResearchDate);

    // providers-eu-cn.md:26 MiniMax M Plan (CN-5), status VERIFIED. Anthropic route, base https://api.minimax.io/anthropic (V).
    // The row names an "M Plan API key" but not its transmission header, so the auth type is ApiKey. The Anthropic route
    // has no OpenAI route flag, so Capabilities is None. The row's OpenAI route https://api.minimax.io/v1 is not seeded.
    public static ProviderEntry MiniMaxAnthropic => new(
        "minimax-m-plan-anthropic",
        ProviderKind.CodingPlan,
        ProviderTransport.AnthropicCompatible,
        ProviderAuthType.ApiKey,
        new Uri("https://api.minimax.io/anthropic"),
        ProviderCapabilities.None,
        new Uri("https://platform.minimax.io/docs/m-plan/intro.md"),
        ResearchDate);
}
