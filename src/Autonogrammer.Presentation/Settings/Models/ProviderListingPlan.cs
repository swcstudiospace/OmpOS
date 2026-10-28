namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>The terms of a listed coding plan as the vendor states them. It describes the plan, never one user's subscription.</summary>
/// <param name="RuleText">The vendor's usage rule, a faithful short quote or close paraphrase.</param>
/// <param name="RuleSourceUrl">The page that states the rule, or null.</param>
/// <param name="InteractiveOnly">True when the vendor limits the plan to interactive coding tools.</param>
/// <param name="SeparateKey">True when the plan key is not interchangeable with the vendor's pay-as-you-go key.</param>
/// <param name="PlanUrl">The page where the plan is described or bought.</param>
/// <param name="Tiers">The purchasable tiers.</param>
/// <param name="ListedTools">The coding tools the vendor lists as supported.</param>
public sealed record ProviderListingPlan(
    string RuleText,
    Uri? RuleSourceUrl,
    bool InteractiveOnly,
    bool SeparateKey,
    Uri PlanUrl,
    IReadOnlyList<ProviderListingTier> Tiers,
    IReadOnlyList<string> ListedTools);
