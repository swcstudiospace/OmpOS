namespace Autonogrammer.Harness.OmpConfig;

/// <summary>
/// The price omp reports for a model, in US dollars per million tokens. Zero is reported for models whose pricing is unknown;
/// see <see cref="OmpModelInfo.PricingStatus"/>.
/// </summary>
/// <param name="Input">The price of input tokens.</param>
/// <param name="Output">The price of output tokens.</param>
/// <param name="CacheRead">The price of cached input tokens that are read.</param>
/// <param name="CacheWrite">The price of input tokens written to the cache.</param>
public sealed record OmpModelCost(double Input, double Output, double CacheRead, double CacheWrite);
