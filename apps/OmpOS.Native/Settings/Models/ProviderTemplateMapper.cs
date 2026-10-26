using System.Collections.Frozen;
using System.Globalization;
using OmpOS.Harness.Providers;
using OmpOS.Presentation.Settings.Models;

namespace OmpOS.Native.Settings.Models;

/// <summary>
/// Maps the rows of <see cref="ProviderCatalogue"/> to the templates a custom provider can start from. Each template carries the
/// source page and the date the catalogue row was read, and a coding plan row carries the published quotas of its plan. A template
/// is only a starting point: saving one writes an ordinary custom provider whose key is added on its card. A template has no
/// authentication header name, because omp's <c>models.yml</c> cannot carry one: the catalogue holds none, and a test keeps it so.
/// </summary>
internal static class ProviderTemplateMapper
{
    private static readonly FrozenDictionary<string, string> DisplayNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ollama-local"] = "Ollama (local)",
        ["lm-studio-local"] = "LM Studio (local)",
        ["llama-cpp-local"] = "llama.cpp server (local)",
        ["vllm-local"] = "vLLM (local)",
        ["localai-local"] = "LocalAI (local)",
        ["jan-local"] = "Jan (local)",
        ["docker-model-runner-local"] = "Docker Model Runner (local)",
        ["ollama-cloud"] = "Ollama Cloud",
        ["perplexity"] = "Perplexity",
        ["openrouter-anthropic"] = "OpenRouter (Anthropic route)",
        ["huggingface-router"] = "Hugging Face Inference Providers",
        ["deepseek-anthropic"] = "DeepSeek (Anthropic route)",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Maps every catalogue row, in the order the catalogue lists them.</summary>
    /// <returns>The templates.</returns>
    public static IReadOnlyList<ProviderTemplate> FromCatalogue() => [.. ProviderCatalogue.All.Select(ToTemplate)];

    /// <summary>Maps one catalogue row.</summary>
    /// <param name="entry">The catalogue row.</param>
    /// <returns>The template.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public static ProviderTemplate ToTemplate(ProviderEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var plan = ProviderCatalogue.FindCodingPlan(entry.Id);
        var group = entry.Kind switch
        {
            ProviderKind.Local => ProviderGroup.Local,
            ProviderKind.CodingPlan => ProviderGroup.CodingPlan,
            ProviderKind.Custom => ProviderGroup.Custom,
            _ => ProviderGroup.Cloud,
        };
        var route = entry.Transport == ProviderTransport.AnthropicCompatible ? "Anthropic route" : "OpenAI route";
        var name = plan is not null
            ? $"{plan.DisplayName} ({route})"
            : DisplayNames.GetValueOrDefault(entry.Id, entry.Id);
        return new ProviderTemplate(
            entry.Id,
            name,
            group,
            entry.Transport == ProviderTransport.AnthropicCompatible ? CustomProviderProtocol.AnthropicCompatible : CustomProviderProtocol.OpenAiCompatible,
            entry.BaseEndpoint,
            entry.AuthType switch
            {
                ProviderAuthType.None => CustomProviderAuth.None,
                ProviderAuthType.BearerToken => CustomProviderAuth.Bearer,
                _ => CustomProviderAuth.ApiKey,
            },
            ToCapabilities(entry.Capabilities),
            entry.SourceUrl,
            entry.RetrievedOn == default ? null : entry.RetrievedOn,
            plan is null ? null : ToPlan(plan),
            Note(entry, plan));
    }

    private static CustomProviderCapabilities ToCapabilities(ProviderCapabilities capabilities)
    {
        var result = CustomProviderCapabilities.None;
        if (capabilities.HasFlag(ProviderCapabilities.ChatCompletions))
        {
            result |= CustomProviderCapabilities.Chat;
        }

        if (capabilities.HasFlag(ProviderCapabilities.Responses))
        {
            result |= CustomProviderCapabilities.Responses;
        }

        if (capabilities.HasFlag(ProviderCapabilities.Embeddings))
        {
            result |= CustomProviderCapabilities.Embeddings;
        }

        return result;
    }

    private static ProviderPlanInfo ToPlan(CodingPlanEntry plan) =>
        new([.. plan.Tiers.Select(tier => new PlanTierInfo(tier.Name, QuotaText(tier)))], plan.ValidityDays, plan.PlanUrl);

    private static string QuotaText(CodingPlanTier tier) =>
        string.Join("; ", tier.Quotas.Select(quota => string.Create(
            CultureInfo.InvariantCulture,
            $"about {quota.Requests:N0} requests per {Window(quota.Window)}")));

    private static string Window(CodingPlanQuotaWindow window) => window switch
    {
        CodingPlanQuotaWindow.FiveHours => "5 hours",
        CodingPlanQuotaWindow.Week => "week",
        CodingPlanQuotaWindow.SubscriptionMonth => "subscription month",
        _ => throw new ArgumentOutOfRangeException(nameof(window), window, "Unknown quota window."),
    };

    private static string? Note(ProviderEntry entry, CodingPlanEntry? plan)
    {
        if (plan is not null)
        {
            var parts = new List<string>();
            if (plan.SeparateKey)
            {
                parts.Add("Use the plan's own key, not the provider's pay-per-use key.");
            }

            if (plan.InteractiveToolsOnly)
            {
                parts.Add("The plan allows use only from interactive coding tools, not from automation.");
            }

            return parts.Count == 0 ? null : string.Join(' ', parts);
        }

        return entry.Kind == ProviderKind.Local ? "Start the server before you test the provider." : null;
    }
}
