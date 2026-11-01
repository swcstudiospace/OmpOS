using System.Threading.RateLimiting;
using Autonogrammer.Api.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace Autonogrammer.Api.Dispatch;

/// <summary>Token-bucket partitions for pairing and connect.</summary>
internal static class DispatchRateLimits
{
    /// <summary>Creates a bucket of <paramref name="limit"/> tokens replenished every <paramref name="period"/>.</summary>
    /// <param name="key">The partition key.</param>
    /// <param name="limit">The token limit.</param>
    /// <param name="period">The replenishment period.</param>
    /// <returns>The partition.</returns>
    public static RateLimitPartition<string> Bucket(string key, int limit, TimeSpan period) =>
        RateLimitPartition.GetTokenBucketLimiter(
            key,
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = limit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                ReplenishmentPeriod = period,
                TokensPerPeriod = limit,
                AutoReplenishment = true,
            });
    /// <summary>Pairing limiter: 5 per hour per caller.</summary>
    internal sealed class DispatchPairingRateLimiterPolicy : IRateLimiterPolicy<string>
    {
        /// <summary>The policy name.</summary>
        public const string PolicyName = "DispatchPairing";

        private readonly ILogger<DispatchPairingRateLimiterPolicy> _logger;

        /// <summary>Initializes a new instance of the <see cref="DispatchPairingRateLimiterPolicy"/> class.</summary>
        /// <param name="logger">The logger.</param>
        public DispatchPairingRateLimiterPolicy(ILogger<DispatchPairingRateLimiterPolicy> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => RateLimitRejection.Handler(_logger);

        /// <inheritdoc />
        public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            return DispatchRateLimits.Bucket("dispatch-pairing:" + RateLimitPartitionKey.For(httpContext), limit: 5, TimeSpan.FromHours(1));
        }
    }

    /// <summary>Connect limiter: 30 per minute, keyed by address and device id so two devices behind one address do not share a bucket.</summary>
    internal sealed class DispatchConnectRateLimiterPolicy : IRateLimiterPolicy<string>
    {
        /// <summary>The policy name.</summary>
        public const string PolicyName = "DispatchConnect";

        private readonly ILogger<DispatchConnectRateLimiterPolicy> _logger;

        /// <summary>Initializes a new instance of the <see cref="DispatchConnectRateLimiterPolicy"/> class.</summary>
        /// <param name="logger">The logger.</param>
        public DispatchConnectRateLimiterPolicy(ILogger<DispatchConnectRateLimiterPolicy> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => RateLimitRejection.Handler(_logger);

        /// <inheritdoc />
        public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            return DispatchRateLimits.Bucket(PartitionKey(httpContext), limit: 30, TimeSpan.FromMinutes(1));
        }

        /// <summary>Builds the connect partition key.</summary>
        /// <param name="httpContext">The request.</param>
        /// <returns>The key, including the device id bounded to 64 characters.</returns>
        public static string PartitionKey(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            httpContext.Request.RouteValues.TryGetValue("id", out var value);
            var id = value?.ToString();
            if (string.IsNullOrEmpty(id))
            {
                id = "-";
            }
            else if (id.Length > 64)
            {
                id = id[..64];
            }

            return "dispatch-connect:" + RateLimitPartitionKey.ForAddress(httpContext) + ":" + id;
        }
    }
}
