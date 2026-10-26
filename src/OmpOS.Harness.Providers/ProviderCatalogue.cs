using System.Collections.Frozen;

namespace OmpOS.Harness.Providers;

/// <summary>
/// The built-in provider rows. Every row is copied from a VERIFIED row of the research files in
/// docs/plans/vision-research/ and cites that row as file:line, the primary source URL and the date the source was read. A
/// provider whose endpoint, auth or quota the research marks unverified has no row. No row stores key material. Where a
/// provider documents more than one route, a row reproduces one route and names it; the other routes are not catalogued.
/// </summary>
public static class ProviderCatalogue
{
    private static readonly DateOnly ResearchDate = new(2026, 10, 22);

    // providers-local-us.md:15 Ollama, status verified. OpenAI-compatible route on the local base http://localhost:11434/v1/.
    // The row says the key is required by the client but ignored locally, so the entry is ApiKey. The Anthropic route
    // of the same row (base http://localhost:11434) is not catalogued.

    /// <summary>Ollama on the user's machine, OpenAI-compatible route. The client must send a key that Ollama ignores.</summary>
    public static ProviderEntry OllamaLocalOpenAi { get; } = new(
        "ollama-local",
        ProviderKind.Local,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.ApiKey,
        new Uri("http://localhost:11434/v1/"),
        ProviderCapabilities.ChatCompletions | ProviderCapabilities.Responses,
        new Uri("https://docs.ollama.com/api/openai-compatibility"),
        ResearchDate);

    // providers-local-us.md:15 Ollama, status verified. OpenAI-compatible route on the cloud base https://ollama.com/v1; Bearer key.

    /// <summary>Ollama's hosted service, OpenAI-compatible route, with a Bearer key.</summary>
    public static ProviderEntry OllamaCloudOpenAi { get; } = new(
        "ollama-cloud",
        ProviderKind.ApiKey,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.BearerToken,
        new Uri("https://ollama.com/v1"),
        ProviderCapabilities.ChatCompletions | ProviderCapabilities.Responses,
        new Uri("https://docs.ollama.com/api/openai-compatibility"),
        ResearchDate);

    // providers-local-us.md:16 LM Studio, status verified. Base http://localhost:1234/v1. Auth is none by default; with
    // Require Authentication on it accepts x-api-key or Bearer, which a user records as a custom provider. The row lists
    // /v1/chat/completions, /v1/responses and /v1/embeddings (and /v1/models and /v1/completions, which have no flag).

    /// <summary>LM Studio's local server, OpenAI-compatible route, with authentication left off as shipped.</summary>
    public static ProviderEntry LmStudioLocalOpenAi { get; } = new(
        "lm-studio-local",
        ProviderKind.Local,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.None,
        new Uri("http://localhost:1234/v1"),
        ProviderCapabilities.ChatCompletions | ProviderCapabilities.Responses | ProviderCapabilities.Embeddings,
        new Uri("https://lmstudio.ai/docs/developer/openai-compat"),
        ResearchDate);

    // providers-local-us.md:17 llama.cpp server, status verified. OpenAI-compatible route on the default base
    // http://127.0.0.1:9931; --api-key flag, header form not stated, so ApiKey.

    /// <summary>The llama.cpp server on its default loopback address, OpenAI-compatible route.</summary>
    public static ProviderEntry LlamaCppLocalOpenAi { get; } = new(
        "llama-cpp-local",
        ProviderKind.Local,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.ApiKey,
        new Uri("http://127.0.0.1:9931"),
        ProviderCapabilities.ChatCompletions | ProviderCapabilities.Responses | ProviderCapabilities.Embeddings,
        new Uri("https://raw.githubusercontent.com/ggml-org/llama.cpp/master/tools/server/README.md"),
        ResearchDate);

    // providers-local-us.md:18 vLLM, status verified. Base http://localhost:8000/v1 (--port default 8000). The --api-key
    // flag is optional and the default start has none, so the entry records no credential. The row lists
    // /v1/chat/completions, /v1/completions, /v1/responses and /v1/embeddings.

    /// <summary>A vLLM server on its default port, OpenAI-compatible route, started without --api-key.</summary>
    public static ProviderEntry VllmLocalOpenAi { get; } = new(
        "vllm-local",
        ProviderKind.Local,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.None,
        new Uri("http://localhost:8000/v1"),
        ProviderCapabilities.ChatCompletions | ProviderCapabilities.Responses | ProviderCapabilities.Embeddings,
        new Uri("https://docs.vllm.ai/en/latest/serving/online_serving/"),
        ResearchDate);

    // providers-local-us.md:19 LocalAI, status verified. Base http://localhost:8080/v1 (Docker example port 8080). The
    // LOCALAI_API_KEY is optional and the default start has none. The row says OpenAI-compatible "Yes" and names no route,
    // so no route flag is recorded.

    /// <summary>A LocalAI server on the port of its Docker example, OpenAI-compatible route, started without an API key.</summary>
    public static ProviderEntry LocalAiLocalOpenAi { get; } = new(
        "localai-local",
        ProviderKind.Local,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.None,
        new Uri("http://localhost:8080/v1"),
        ProviderCapabilities.None,
        new Uri("https://localai.io/docs/basics/getting_started/"),
        ResearchDate);

    // providers-local-us.md:20 Jan, status verified (OpenAI-compatible only). Base http://127.0.0.1:1337/v1 (default host
    // and port). The API key is set in Jan settings and there is none if unset, so the entry records no credential. The
    // row names no route, so no route flag is recorded.

    /// <summary>Jan's local API server on its default address, OpenAI-compatible route, with no key set.</summary>
    public static ProviderEntry JanLocalOpenAi { get; } = new(
        "jan-local",
        ProviderKind.Local,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.None,
        new Uri("http://127.0.0.1:1337/v1"),
        ProviderCapabilities.None,
        new Uri("https://docs.jan.ai/desktop/api-server"),
        ResearchDate);

    // providers-local-us.md:21 Docker Model Runner, status verified. OpenAI route http://localhost:12434/engines/v1; the
    // Anthropic and Ollama formats on http://localhost:12434 and the container host are not catalogued. The row lists
    // /engines/v1/chat/completions, embeddings and models. Auth is "not stated on opened page (unverified)", so the entry
    // records no credential rather than inventing one.

    /// <summary>Docker Model Runner on the host, OpenAI-compatible route.</summary>
    public static ProviderEntry DockerModelRunnerLocalOpenAi { get; } = new(
        "docker-model-runner-local",
        ProviderKind.Local,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.None,
        new Uri("http://localhost:12434/engines/v1"),
        ProviderCapabilities.ChatCompletions | ProviderCapabilities.Embeddings,
        new Uri("https://docs.docker.com/ai/model-runner/api-reference/"),
        ResearchDate);

    // providers-local-us.md:36 Perplexity, status verified. Base https://api.perplexity.ai/v1; Responses format only
    // (chat completions not confirmed); API key, header not stated, so ApiKey.

    /// <summary>Perplexity's hosted API, Responses route only.</summary>
    public static ProviderEntry Perplexity { get; } = new(
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
    // OpenAI-compatible base https://openrouter.ai/api/v1 is not catalogued.

    /// <summary>OpenRouter's Anthropic-compatible route for Claude Code, with a Bearer token.</summary>
    public static ProviderEntry OpenRouterAnthropic { get; } = new(
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
    // it, so no route flag is recorded and Capabilities is None. Billing is pay-as-you-go beyond the PRO credits, so the
    // row has no coding plan.

    /// <summary>The Hugging Face Inference Providers router, OpenAI-compatible route, with an HF token.</summary>
    public static ProviderEntry HuggingFaceRouter { get; } = new(
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
    // https://api.deepseek.com is not catalogued. The row records pay-as-you-go pricing only, so there is no coding plan.

    /// <summary>DeepSeek's Anthropic-compatible route, pay as you go, with a Bearer key.</summary>
    public static ProviderEntry DeepSeekAnthropic { get; } = new(
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
    // has no OpenAI route flag, so Capabilities is None. The row's OpenAI route https://api.minimax.io/v1 is not catalogued.

    /// <summary>MiniMax's M Plan coding plan, Anthropic-compatible route, with the plan's own key.</summary>
    public static ProviderEntry MiniMaxAnthropic { get; } = new(
        "minimax-m-plan-anthropic",
        ProviderKind.CodingPlan,
        ProviderTransport.AnthropicCompatible,
        ProviderAuthType.ApiKey,
        new Uri("https://api.minimax.io/anthropic"),
        ProviderCapabilities.None,
        new Uri("https://platform.minimax.io/docs/m-plan/intro.md"),
        ResearchDate);

    // providers-eu-cn.md:30 iFlytek Spark / Astron MaaS Coding Plan (CN-9), status VERIFIED, re-read on the primary page
    // https://www.xfyun.cn/doc/spark/CodingPlan.html (v2.3, section 1.2). OpenAI base .../v2. The Claude Code template in
    // section 2.2 hands the plan key to ANTHROPIC_AUTH_TOKEN, and the OpenAI template puts it in "apiKey", whose header
    // the page does not state. The page names the /v2 route "openai-completions" in its OpenClaw template, hence the
    // ChatCompletions flag. The row's Responses route (.../v1/responses) is not under /v2 and is not catalogued.

    /// <summary>iFlytek's Astron Coding Plan, OpenAI-compatible route, with the plan's own key.</summary>
    public static ProviderEntry IFlytekCodingOpenAi { get; } = new(
        "iflytek-astron-coding-openai",
        ProviderKind.CodingPlan,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.ApiKey,
        new Uri("https://maas-coding-api.cn-huabei-1.xf-yun.com/v2"),
        ProviderCapabilities.ChatCompletions,
        new Uri("https://www.xfyun.cn/doc/spark/CodingPlan.html"),
        ResearchDate);

    // providers-eu-cn.md:30 iFlytek (CN-9). Anthropic base https://maas-coding-api.cn-huabei-1.xf-yun.com/anthropic,
    // read in full on the primary page (section 1.2); plan key sent as ANTHROPIC_AUTH_TOKEN (section 2.2), i.e. Bearer.

    /// <summary>iFlytek's Astron Coding Plan, Anthropic-compatible route, with the plan's own key as a Bearer token.</summary>
    public static ProviderEntry IFlytekCodingAnthropic { get; } = new(
        "iflytek-astron-coding-anthropic",
        ProviderKind.CodingPlan,
        ProviderTransport.AnthropicCompatible,
        ProviderAuthType.BearerToken,
        new Uri("https://maas-coding-api.cn-huabei-1.xf-yun.com/anthropic"),
        ProviderCapabilities.None,
        new Uri("https://www.xfyun.cn/doc/spark/CodingPlan.html"),
        ResearchDate);

    // providers-local-us.md:23 Generic OpenAI-compatible endpoint (pattern), verified in the Cerebras docs: the user supplies
    // the /v1 root and a Bearer API key.

    /// <summary>The generic pattern for a user-supplied OpenAI-compatible endpoint, local or remote.</summary>
    public static ProviderPattern GenericOpenAiCompatible { get; } = new(
        "openai-compatible-generic",
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.BearerToken,
        new Uri("https://inference-docs.cerebras.ai/resources/openai"),
        ResearchDate);

    // providers-local-us.md:24 Generic Anthropic-compatible endpoint (pattern): ANTHROPIC_BASE_URL is the root with no /v1;
    // ANTHROPIC_AUTH_TOKEN is sent as Bearer and ANTHROPIC_API_KEY as x-api-key.

    /// <summary>The generic pattern for a user-supplied Anthropic-compatible endpoint, local or remote.</summary>
    public static ProviderPattern GenericAnthropicCompatible { get; } = new(
        "anthropic-compatible-generic",
        ProviderTransport.AnthropicCompatible,
        ProviderAuthType.BearerToken,
        new Uri("https://docs.ollama.com/api/anthropic-compatibility"),
        ResearchDate);

    // providers-eu-cn.md:26 MiniMax M Plan (CN-5), VERIFIED. Tiers Go, Explore and Build with 1x, 3x and 7.5x the Go usage
    // (V); "usage caps unpublished (V)", so no quota is recorded. Official tool list: Claude Code, Codex, Cursor, OpenCode, Pi,
    // Hermes Agent, OpenClaw (V). The validity period is not stated. Plans allow use from listed coding tools only
    // (providers-eu-cn.md:38, owner decision 1, which names MiniMax).

    /// <summary>MiniMax's M Plan: three tiers, usage caps unpublished, a separate plan key.</summary>
    public static CodingPlanEntry MiniMaxMPlan { get; } = new(
        "minimax-m-plan",
        "MiniMax M Plan",
        ["minimax-m-plan-anthropic"],
        [new CodingPlanTier("Go", []), new CodingPlanTier("Explore", []), new CodingPlanTier("Build", [])],
        null,
        SeparateKey: true,
        InteractiveToolsOnly: true,
        ["Claude Code", "Codex", "Cursor", "OpenCode", "Pi", "Hermes Agent", "OpenClaw"],
        new Uri("https://platform.minimax.io/docs/m-plan/intro.md"),
        new Uri("https://platform.minimax.io/docs/m-plan/intro.md"),
        ResearchDate);

    // providers-eu-cn.md:30 iFlytek Astron Coding Plan (CN-9), VERIFIED; limits and validity re-read on the primary page
    // (section 4.1): 高效版 up to about 6,000 requests per 5 hours, 45,000 per week and 90,000 per subscription month; 速通版 up
    // to about 30,000 per subscription month; each paid period is valid for 31 days; the key is the plan's own, issued on the
    // package subscription page; use is limited to interactive coding tools (section 4.1.4).

    /// <summary>iFlytek's Astron Coding Plan: two tiers with published request limits, 31-day validity, a separate plan key.</summary>
    public static CodingPlanEntry IFlytekAstronPlan { get; } = new(
        "iflytek-astron-coding",
        "iFlytek Astron Coding Plan",
        ["iflytek-astron-coding-openai", "iflytek-astron-coding-anthropic"],
        [
            new CodingPlanTier(
                "高效版",
                [
                    new CodingPlanQuota(CodingPlanQuotaWindow.FiveHours, 6_000),
                    new CodingPlanQuota(CodingPlanQuotaWindow.Week, 45_000),
                    new CodingPlanQuota(CodingPlanQuotaWindow.SubscriptionMonth, 90_000),
                ]),
            new CodingPlanTier("速通版", [new CodingPlanQuota(CodingPlanQuotaWindow.SubscriptionMonth, 30_000)]),
        ],
        31,
        SeparateKey: true,
        InteractiveToolsOnly: true,
        ["Claude Code", "OpenCode", "Cursor", "OpenClaw"],
        new Uri("https://maas.xfyun.cn/packageSubscription"),
        new Uri("https://www.xfyun.cn/doc/spark/CodingPlan.html"),
        ResearchDate);

    /// <summary>Every built-in provider entry, local runtimes first, then hosted API-key rows, then coding plans.</summary>
    public static IReadOnlyList<ProviderEntry> All { get; } =
    [
        OllamaLocalOpenAi,
        LmStudioLocalOpenAi,
        LlamaCppLocalOpenAi,
        VllmLocalOpenAi,
        LocalAiLocalOpenAi,
        JanLocalOpenAi,
        DockerModelRunnerLocalOpenAi,
        OllamaCloudOpenAi,
        Perplexity,
        OpenRouterAnthropic,
        HuggingFaceRouter,
        DeepSeekAnthropic,
        MiniMaxAnthropic,
        IFlytekCodingOpenAi,
        IFlytekCodingAnthropic,
    ];

    /// <summary>The built-in coding plans. Each plan has its own key, quota and error mapping apart from the pay-per-use rows.</summary>
    public static IReadOnlyList<CodingPlanEntry> CodingPlans { get; } = [MiniMaxMPlan, IFlytekAstronPlan];

    private static readonly FrozenDictionary<string, ProviderEntry> ById =
        All.ToFrozenDictionary(entry => entry.Id, StringComparer.Ordinal);

    /// <summary>Returns the built-in entry with the id, or null when there is none.</summary>
    /// <param name="id">The entry id.</param>
    public static ProviderEntry? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return ById.GetValueOrDefault(id);
    }

    /// <summary>Returns the coding plan that owns the provider entry, or null when the entry belongs to no plan.</summary>
    /// <param name="providerId">The id of a provider entry.</param>
    public static CodingPlanEntry? FindCodingPlan(string providerId)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        foreach (var plan in CodingPlans)
        {
            if (plan.ProviderIds.Contains(providerId, StringComparer.Ordinal))
            {
                return plan;
            }
        }

        return null;
    }
}
