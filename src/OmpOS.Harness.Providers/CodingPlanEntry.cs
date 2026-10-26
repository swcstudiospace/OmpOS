namespace OmpOS.Harness.Providers;

/// <summary>
/// A subscription plan whose separate coding key is used from listed coding tools. A plan is catalogued apart from the
/// pay-per-use rows because it has its own key, its own quota windows and its own error mapping. It is a description of the
/// plan, never of one user's subscription, and holds no key material. Each provider entry the plan exposes is a row of
/// <see cref="ProviderCatalogue"/> with kind <see cref="ProviderKind.CodingPlan"/>.
/// </summary>
/// <param name="Id">The slug that names the plan.</param>
/// <param name="DisplayName">The plan name as the provider publishes it.</param>
/// <param name="ProviderIds">The ids of the catalogue entries that reach the plan, one per route.</param>
/// <param name="Tiers">The purchasable tiers with their published request limits.</param>
/// <param name="ValidityDays">How many days a paid period stays valid, or null when the research records none.</param>
/// <param name="SeparateKey">True when the plan's key is not interchangeable with the provider's pay-per-use key.</param>
/// <param name="InteractiveToolsOnly">True when the plan allows use only from interactive coding tools and forbids automation.</param>
/// <param name="ListedTools">The coding tools the provider lists as supported.</param>
/// <param name="PlanUrl">The page where the plan is described, bought or renewed.</param>
/// <param name="SourceUrl">The primary page the facts were read on.</param>
/// <param name="RetrievedOn">The date the source was read.</param>
public sealed record CodingPlanEntry(
    string Id,
    string DisplayName,
    IReadOnlyList<string> ProviderIds,
    IReadOnlyList<CodingPlanTier> Tiers,
    int? ValidityDays,
    bool SeparateKey,
    bool InteractiveToolsOnly,
    IReadOnlyList<string> ListedTools,
    Uri PlanUrl,
    Uri SourceUrl,
    DateOnly RetrievedOn);
