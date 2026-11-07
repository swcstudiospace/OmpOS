using System.Threading.RateLimiting;
using Autonogrammer.Api.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace Autonogrammer.Api.DataPlane;

/// <summary>
/// Token-bucket limiter of <c>GET /status/data-plane</c>: one bucket per caller (<see cref="RateLimitPartitionKey.For"/>, the subject or else the address) of
/// <see cref="RequestsPerMinute"/> requests a minute. Rejections answer 429 with a Retry-After hint.
/// </summary>
internal sealed class DataPlaneStatusRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <summary>The requests per minute one caller may make.</summary>
    public const int RequestsPerMinute = 30;

    private readonly ILogger<DataPlaneStatusRateLimiterPolicy> _logger;

    /// <summary>Initializes a new instance of the <see cref="DataPlaneStatusRateLimiterPolicy"/> class.</summary>
    /// <param name="logger">The logger.</param>
    public DataPlaneStatusRateLimiterPolicy(ILogger<DataPlaneStatusRateLimiterPolicy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => RateLimitRejection.Handler(_logger);

    /// <inheritdoc />
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return RateLimitPartition.GetTokenBucketLimiter(
            "data-plane-status:" + RateLimitPartitionKey.For(httpContext),
            static _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = RequestsPerMinute,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                TokensPerPeriod = RequestsPerMinute,
                AutoReplenishment = true,
            });
    }
}
