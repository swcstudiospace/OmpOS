using Autonogrammer.Presentation.Settings.Models;

namespace Autonogrammer.Presentation.Tests.Settings.Models;

/// <summary>Sample providers for the Models page tests: one row of every group.</summary>
internal static class ModelsTestData
{
    /// <summary>A fake key. It matches no real provider's key format.</summary>
    public const string FakeKey = "sk-test-FAKE-0123456789abcdef";

    /// <summary>Ollama on this machine: local, no credential needed.</summary>
    public static ProviderInfo Ollama() => new(
        "ollama-local",
        "Ollama (local)",
        ProviderGroup.Local,
        "http://localhost:11434/v1/",
        ProviderAuthKind.None,
        CredentialState.NotRequired,
        ["ollama-local/llama3"],
        new Uri("https://docs.ollama.com/api/openai-compatibility"),
        new DateOnly(2026, 10, 22),
        null,
        false,
        null);

    /// <summary>A cloud API with no key yet.</summary>
    public static ProviderInfo Perplexity(CredentialState credential = CredentialState.Absent) => new(
        "perplexity",
        "Perplexity",
        ProviderGroup.Cloud,
        "https://api.perplexity.ai",
        ProviderAuthKind.ApiKey,
        credential,
        ["perplexity/sonar"],
        new Uri("https://docs.perplexity.ai"),
        new DateOnly(2026, 10, 22),
        null,
        false,
        null);

    /// <summary>A coding plan row with two tiers and a validity period.</summary>
    public static ProviderInfo MiniMaxPlan() => new(
        "minimax-m-plan",
        "MiniMax M plan",
        ProviderGroup.CodingPlan,
        "https://api.minimax.example/anthropic",
        ProviderAuthKind.ApiKey,
        CredentialState.Absent,
        ["minimax-m-plan/m2"],
        new Uri("https://platform.minimax.example/docs"),
        new DateOnly(2026, 10, 22),
        new ProviderPlanInfo(
            [new PlanTierInfo("Starter", "about 100 requests per 5 hours"), new PlanTierInfo("Max", string.Empty)],
            30,
            new Uri("https://platform.minimax.example/plan")),
        false,
        null);

    /// <summary>A custom provider Autonogrammer wrote.</summary>
    public static ProviderInfo Gateway()
    {
        var form = GatewayForm();
        return new ProviderInfo(
            form.Id,
            form.DisplayName,
            ProviderGroup.Custom,
            form.BaseEndpoint.OriginalString,
            ProviderAuthKind.ApiKey,
            CredentialState.Absent,
            [.. form.Models.Select(model => $"{form.Id}/{model}")],
            null,
            null,
            null,
            true,
            null,
            form);
    }

    /// <summary>The saved definition of <see cref="Gateway"/>.</summary>
    public static CustomProviderForm GatewayForm() => new(
        "My gateway",
        "my-gateway",
        CustomProviderProtocol.OpenAiCompatible,
        new Uri("https://gateway.example.com/v1"),
        CustomProviderAuth.ApiKey,
        ["fast", "big"],
        CustomProviderCapabilities.Chat,
        false);

    /// <summary>A provider omp knows by itself, with a key and two models.</summary>
    public static ProviderInfo OpenAi(CredentialState credential = CredentialState.Present) => new(
        "openai",
        "OpenAI",
        ProviderGroup.OmpBuiltIn,
        string.Empty,
        ProviderAuthKind.ApiKey,
        credential,
        ["openai/gpt-4o-mini", "openai/gpt-4o"],
        null,
        null,
        null,
        false,
        null);

    /// <summary>A provider omp signs in to by itself.</summary>
    public static ProviderInfo SignInOnly() => new(
        "anthropic",
        "Anthropic",
        ProviderGroup.OmpBuiltIn,
        string.Empty,
        ProviderAuthKind.OAuth,
        CredentialState.Absent,
        ["anthropic/claude-sonnet"],
        null,
        null,
        null,
        false,
        null);

    /// <summary>The five groups in one list.</summary>
    public static ProviderInfo[] OneOfEveryGroup() => [Ollama(), Perplexity(), MiniMaxPlan(), Gateway(), OpenAi()];

    /// <summary>A local template: Ollama on loopback, no authentication, verified on a page.</summary>
    public static ProviderTemplate OllamaTemplate() => new(
        "ollama-local",
        "Ollama",
        ProviderGroup.Local,
        CustomProviderProtocol.OpenAiCompatible,
        new Uri("http://localhost:11434/v1"),
        CustomProviderAuth.None,
        CustomProviderCapabilities.Chat | CustomProviderCapabilities.Embeddings,
        new Uri("https://docs.ollama.com/api/openai-compatibility"),
        new DateOnly(2026, 10, 22),
        null,
        "Start Ollama before testing the connection.");

    /// <summary>A cloud template: a hosted OpenAI-compatible API with a key.</summary>
    public static ProviderTemplate CloudTemplate() => new(
        "example-cloud",
        "Example Cloud",
        ProviderGroup.Cloud,
        CustomProviderProtocol.OpenAiCompatible,
        new Uri("https://api.example-cloud.test/v1"),
        CustomProviderAuth.ApiKey,
        CustomProviderCapabilities.Chat | CustomProviderCapabilities.Responses,
        new Uri("https://docs.example-cloud.test/api"),
        new DateOnly(2026, 10, 22),
        null,
        null);

    /// <summary>A coding plan template: an Anthropic-compatible endpoint with a bearer token, a plan, and no source.</summary>
    public static ProviderTemplate PlanTemplate() => new(
        "example-plan",
        "Example coding plan",
        ProviderGroup.CodingPlan,
        CustomProviderProtocol.AnthropicCompatible,
        new Uri("https://plan.example-cloud.test/anthropic"),
        CustomProviderAuth.Bearer,
        CustomProviderCapabilities.Chat,
        null,
        null,
        new ProviderPlanInfo([new PlanTierInfo("Starter", "about 100 requests per 5 hours")], 30, new Uri("https://plan.example-cloud.test/plan")),
        null);

    /// <summary>A template whose endpoint the user has to supply.</summary>
    public static ProviderTemplate OpenEndpointTemplate() => new(
        "bring-your-own",
        "Bring your own endpoint",
        ProviderGroup.Cloud,
        CustomProviderProtocol.OpenAiCompatible,
        null,
        CustomProviderAuth.Bearer,
        CustomProviderCapabilities.Chat,
        new Uri("https://docs.example-cloud.test/byo"),
        null,
        null,
        null);

    /// <summary>The four templates in one list.</summary>
    public static ProviderTemplate[] AllTemplates() => [OllamaTemplate(), CloudTemplate(), PlanTemplate(), OpenEndpointTemplate()];
}
