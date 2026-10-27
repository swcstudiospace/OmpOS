namespace OmpOS.Harness.Providers;

/// <summary>
/// The terms of a coding plan as the vendor states them. A plan key is a bring-your-own-key credential: OmpOS shows the rule text
/// beside the key field and sends traffic only to the plan's own host. It describes the plan, never one user's subscription.
/// </summary>
/// <param name="RuleText">The vendor's usage rule: a faithful short quote or a close paraphrase of the cited page.</param>
/// <param name="RuleSourceUrl">The page that states the rule, or null.</param>
/// <param name="InteractiveOnly">True when the vendor limits the plan to interactive coding tools and forbids automation or backends.</param>
/// <param name="CloudDefaultPayAsYouGo">True when OmpOS cloud agents use the vendor's pay-as-you-go key instead of the plan key. It is true for every plan.</param>
/// <param name="SeparateKey">True when the plan key cannot be used for the vendor's pay-as-you-go API, or the reverse.</param>
/// <param name="PlanUrl">The page where the plan is described or bought.</param>
/// <param name="ValidityDays">How many days a paid period stays valid, or null when not recorded.</param>
/// <param name="Tiers">The purchasable tiers.</param>
/// <param name="ListedTools">The coding tools the vendor lists as supported.</param>
public sealed record CataloguePlanTerms(
    string RuleText,
    Uri? RuleSourceUrl,
    bool InteractiveOnly,
    bool CloudDefaultPayAsYouGo,
    bool SeparateKey,
    Uri PlanUrl,
    int? ValidityDays,
    IReadOnlyList<CataloguePlanTier> Tiers,
    IReadOnlyList<string> ListedTools);
