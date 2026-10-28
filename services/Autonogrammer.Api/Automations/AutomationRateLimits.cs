using System.Threading.RateLimiting;

namespace Autonogrammer.Api.Automations;

/// <summary>Builds the token-bucket partitions of the automation rate-limit policies.</summary>
internal static class AutomationRateLimits
{
    /// <summary>Creates a one-bucket partition: <paramref name="limit"/> requests per minute across all callers.</summary>
    /// <param name="key">The partition key.</param>
    /// <param name="limit">The requests per minute.</param>
    /// <returns>The partition.</returns>
    public static RateLimitPartition<string> Bucket(string key, int limit) =>
        RateLimitPartition.GetTokenBucketLimiter(
            key,
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = limit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                TokensPerPeriod = limit,
                AutoReplenishment = true,
            });
}
