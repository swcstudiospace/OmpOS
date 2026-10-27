using OmpOS.Presentation.Settings.Models;

namespace OmpOS.Native.Tests.Settings;

/// <summary>Provider listings the Models page view tests draw: a native API, an account-specific API, an unverified API, a plan and a sign-in plan. None holds a key.</summary>
internal static class ModelsListingData
{
    private static readonly DateOnly Day = new(2026, 10, 27);

    private static ProviderListingRoute Route(string id, string api, string url, bool discovers = true, bool needsModels = false) =>
        new(id, api, url, "Bearer", true, discovers, needsModels, []);

    /// <summary>The listings, in source order.</summary>
    internal static ProviderListing[] All() =>
    [
        new(
            "mistral", "Mistral AI La Plateforme", ProviderListingRegion.Europe, "FR", null, ProviderListingKind.Api, "mistral", "MISTRAL_API_KEY", null,
            [Route("openai", "openai-completions", "https://api.mistral.ai/v1")], [], null, null, new Uri("https://docs.mistral.ai/api"),
            ProviderListingStatus.Verified, Day, null, null),
        new(
            "infomaniak-ai-tools", "Infomaniak AI Tools", ProviderListingRegion.Europe, "CH", "Switzerland (non-EU)", ProviderListingKind.Api, null, "INFOMANIAK_API_KEY", null,
            [Route("openai", "openai-completions", "https://api.infomaniak.com/2/ai/{product_id}/openai/v1")],
            [new ProviderListingVariable("product_id", "Product ID", "123456", null)], null, null, new Uri("https://developer.infomaniak.com/docs/api"),
            ProviderListingStatus.Partial, Day, null, null),
        new(
            "anthropic-subscription", "Claude Pro / Max login", ProviderListingRegion.UnitedStates, "US", null, ProviderListingKind.CodingPlan, null, null, "/login anthropic",
            [], [], null, null, new Uri("https://code.claude.com/docs/en/legal-and-compliance"), ProviderListingStatus.Verified, Day, null,
            new ProviderListingPlan(
                "OAuth is intended exclusively for ordinary use of Claude Code and other native Anthropic applications.", null, true, false,
                new Uri("https://claude.com/pricing"), [new ProviderListingTier("Pro", "$20 per month", true, null)], [])),
        new(
            "baichuan", "Baichuan Intelligence", ProviderListingRegion.China, "CN", null, ProviderListingKind.Api, null, "BAICHUAN_API_KEY", null,
            [Route("openai", "openai-completions", "https://api.baichuan-ai.com/v1", discovers: false, needsModels: true)], [], null, null,
            new Uri("https://platform.baichuan-ai.com/docs/api"), ProviderListingStatus.Unverified, Day, "Verify before use: the docs page returned only navigation.", null),
        new(
            "zai", "GLM Coding Plan (Z.ai)", ProviderListingRegion.China, "CN", null, ProviderListingKind.CodingPlan, "zai", "ZAI_API_KEY", null,
            [Route("openai", "openai-completions", "https://api.z.ai/api/coding/paas/v4", discovers: false)], [], null, null,
            new Uri("https://docs.z.ai/devpack/usage-policy.md"), ProviderListingStatus.Verified, Day, null,
            new ProviderListingPlan(
                "Only in officially supported tools; bulk or automated usage and resale are banned.", new Uri("https://docs.z.ai/devpack/usage-policy.md"), true, true,
                new Uri("https://z.ai/subscribe"), [new ProviderListingTier("Lite", "$18 per month", true, "2,000 credits per 5 hours")], [])),
    ];
}
