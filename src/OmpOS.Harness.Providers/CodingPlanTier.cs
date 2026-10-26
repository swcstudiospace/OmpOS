namespace OmpOS.Harness.Providers;

/// <summary>
/// One purchasable tier of a coding plan. An empty quota list means the plan publishes no request limit for the tier, not
/// that the tier is unlimited.
/// </summary>
/// <param name="Name">The tier name as the provider publishes it.</param>
/// <param name="Quotas">The published request limits, shortest window first.</param>
public sealed record CodingPlanTier(string Name, IReadOnlyList<CodingPlanQuota> Quotas);
