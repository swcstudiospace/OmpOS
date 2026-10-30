using Autonogrammer.Presentation.Settings.Models;

namespace Autonogrammer.Presentation.Tests.Settings.Models;

/// <summary>Sample provider listings for the directory tests: one of each region and kind, a plan, a sign-in row and a two-route row.</summary>
internal static class ListingTestData
{
    private static readonly DateOnly Day = new(2026, 10, 27);

    private static ProviderListingRoute Route(string id, string api, string url, bool takesKey = true, bool discovers = true, bool needsModels = false, IReadOnlyList<string>? models = null) =>
        new(id, api, url, takesKey ? "Bearer" : "none", takesKey, discovers, needsModels, models ?? []);

    /// <summary>Mistral's pay-as-you-go API: European, omp knows it, models are discovered.</summary>
    public static ProviderListing Mistral() => new(
        "mistral",
        "Mistral AI La Plateforme",
        ProviderListingRegion.Europe,
        "FR",
        "EU hosting is inferred, not stated by the vendor",
        ProviderListingKind.Api,
        "mistral",
        "MISTRAL_API_KEY",
        null,
        [Route("openai", "openai-completions", "https://api.mistral.ai/v1")],
        [],
        new Uri("https://docs.mistral.ai/api"),
        new Uri("https://console.mistral.ai/home"),
        new Uri("https://docs.mistral.ai/openapi.yaml"),
        ProviderListingStatus.Verified,
        Day,
        null,
        null);

    /// <summary>Infomaniak AI Tools: Swiss, omp does not know it, the base URL holds the account's product id.</summary>
    public static ProviderListing Infomaniak() => new(
        "infomaniak-ai-tools",
        "Infomaniak AI Tools",
        ProviderListingRegion.Europe,
        "CH",
        "Switzerland (non-EU)",
        ProviderListingKind.Api,
        null,
        "INFOMANIAK_API_KEY",
        null,
        [Route("openai", "openai-completions", "https://api.infomaniak.com/2/ai/{product_id}/openai/v1")],
        [new ProviderListingVariable("product_id", "Product ID", "123456", "From GET /1/ai")],
        null,
        null,
        new Uri("https://developer.infomaniak.com/docs/api"),
        ProviderListingStatus.Partial,
        Day,
        "Docs page is a JS shell; facts from search excerpts.",
        null);

    /// <summary>Perplexity: American, omp does not know it, the vendor documents no models list.</summary>
    public static ProviderListing Perplexity() => new(
        "perplexity",
        "Perplexity API",
        ProviderListingRegion.UnitedStates,
        "US",
        null,
        ProviderListingKind.Api,
        null,
        "PERPLEXITY_API_KEY",
        null,
        [Route("responses", "openai-responses", "https://api.perplexity.ai/v1", discovers: false, needsModels: true)],
        [],
        null,
        null,
        new Uri("https://docs.perplexity.ai"),
        ProviderListingStatus.Verified,
        Day,
        null,
        null);

    /// <summary>The Z.ai GLM coding plan: Chinese, interactive use only, a separate key, two tiers one of them with an unverified price.</summary>
    public static ProviderListing ZaiPlan() => new(
        "zai",
        "GLM Coding Plan (Z.ai)",
        ProviderListingRegion.China,
        "CN",
        null,
        ProviderListingKind.CodingPlan,
        "zai",
        "ZAI_API_KEY",
        null,
        [Route("openai", "openai-completions", "https://api.z.ai/api/coding/paas/v4", discovers: false, needsModels: false, models: ["glm-5.3"])],
        [],
        new Uri("https://docs.z.ai/devpack/overview"),
        new Uri("https://z.ai/manage-apikey/coding-plan/personal/my-plan"),
        new Uri("https://docs.z.ai/devpack/usage-policy.md"),
        ProviderListingStatus.Verified,
        Day,
        null,
        new ProviderListingPlan(
            "Only in officially supported tools; bulk or automated usage and resale are banned.",
            new Uri("https://docs.z.ai/devpack/usage-policy.md"),
            InteractiveOnly: true,
            SeparateKey: true,
            new Uri("https://z.ai/subscribe"),
            [
                new ProviderListingTier("Lite", "$18 per month", true, "2,000 credits per 5 hours"),
                new ProviderListingTier("Pro", "price unverified", false, null),
            ],
            ["Claude Code", "Cline"]));

    /// <summary>Claude Pro and Max sign-in: a credential the user supplies in omp; Autonogrammer never stores it.</summary>
    public static ProviderListing AnthropicSubscription() => new(
        "anthropic-subscription",
        "Claude Pro / Max login",
        ProviderListingRegion.UnitedStates,
        "US",
        null,
        ProviderListingKind.CodingPlan,
        null,
        null,
        "/login anthropic",
        [],
        [],
        null,
        null,
        new Uri("https://code.claude.com/docs/en/legal-and-compliance"),
        ProviderListingStatus.Verified,
        Day,
        null,
        new ProviderListingPlan(
            "OAuth is intended exclusively for ordinary use of Claude Code and other native Anthropic applications.",
            null,
            InteractiveOnly: true,
            SeparateKey: false,
            new Uri("https://claude.com/pricing"),
            [new ProviderListingTier("Pro", "$20 per month", true, null)],
            []));

    /// <summary>Ollama on this machine: omp finds it itself, no key.</summary>
    public static ProviderListing Ollama() => new(
        "ollama",
        "Ollama",
        ProviderListingRegion.Local,
        null,
        null,
        ProviderListingKind.Local,
        "ollama",
        null,
        null,
        [Route("openai", "openai-responses", "http://localhost:11434/v1", takesKey: false)],
        [],
        null,
        null,
        new Uri("https://docs.ollama.com/api/openai-compatibility"),
        ProviderListingStatus.Verified,
        Day,
        null,
        null);

    /// <summary>LocalAI: local, omp does not know it, keyless, so Autonogrammer writes a block.</summary>
    public static ProviderListing LocalAi() => Ollama() with
    {
        Id = "localai",
        DisplayName = "LocalAI",
        OmpProviderId = null,
        Routes = [Route("openai", "openai-completions", "http://localhost:8080/v1", takesKey: false)],
    };

    /// <summary>An unverified Chinese API: the card must say to verify before use.</summary>
    public static ProviderListing Unverified() => new(
        "baichuan",
        "Baichuan Intelligence",
        ProviderListingRegion.China,
        "CN",
        null,
        ProviderListingKind.Api,
        null,
        "BAICHUAN_API_KEY",
        null,
        [Route("openai", "openai-completions", "https://api.baichuan-ai.com/v1", discovers: false, needsModels: true)],
        [],
        null,
        null,
        new Uri("https://platform.baichuan-ai.com/docs/api"),
        ProviderListingStatus.Unverified,
        Day,
        "Verify before use: the docs page returned only navigation.",
        null);

    /// <summary>DeepSeek: Chinese, omp knows it, two routes (OpenAI and Anthropic).</summary>
    public static ProviderListing DeepSeek() => new(
        "deepseek",
        "DeepSeek API",
        ProviderListingRegion.China,
        "CN",
        null,
        ProviderListingKind.Api,
        "deepseek",
        "DEEPSEEK_API_KEY",
        null,
        [
            Route("openai", "openai-completions", "https://api.deepseek.com"),
            Route("anthropic", "anthropic-messages", "https://api.deepseek.com/anthropic", discovers: false, needsModels: true),
        ],
        [],
        new Uri("https://api-docs.deepseek.com/"),
        new Uri("https://platform.deepseek.com/api_keys"),
        new Uri("https://api-docs.deepseek.com/"),
        ProviderListingStatus.Verified,
        Day,
        null,
        null);

    /// <summary>The whole sample, in a deliberately unsorted order so the directory must sort it.</summary>
    public static ProviderListing[] All() =>
    [
        Perplexity(),
        ZaiPlan(),
        Ollama(),
        Mistral(),
        Unverified(),
        AnthropicSubscription(),
        Infomaniak(),
        DeepSeek(),
        LocalAi(),
    ];
}
