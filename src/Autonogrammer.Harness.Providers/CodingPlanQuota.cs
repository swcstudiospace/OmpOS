namespace Autonogrammer.Harness.Providers;

/// <summary>One published request limit of a plan tier. Plans publish the limit as an approximate maximum.</summary>
/// <param name="Window">The period the limit counts over.</param>
/// <param name="Requests">The approximate maximum number of requests in that period.</param>
public sealed record CodingPlanQuota(CodingPlanQuotaWindow Window, int Requests);
