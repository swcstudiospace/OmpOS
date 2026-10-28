namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>What the catalogue says about a coding plan. It describes the plan, never one user's subscription.</summary>
/// <param name="Tiers">The purchasable tiers with their published limits.</param>
/// <param name="ValidityDays">How many days a paid period stays valid, or null when the research records none.</param>
/// <param name="PlanUrl">The page where the plan is described, bought or renewed.</param>
public sealed record ProviderPlanInfo(IReadOnlyList<PlanTierInfo> Tiers, int? ValidityDays, Uri PlanUrl);
