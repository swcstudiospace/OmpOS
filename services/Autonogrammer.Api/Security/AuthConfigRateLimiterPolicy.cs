using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Autonogrammer.Api.Security;

/// <summary>
/// Token-bucket limiter of the anonymous <c>GET /auth/config</c> endpoint: one bucket per remote address
/// (<see cref="RateLimitPartitionKey.ForAddress"/>) of <see cref="RequestsPerMinute"/> requests a minute. The endpoint is anonymous,
/// so the address is the only key; the partitioned limiter disposes idle buckets, which bounds memory. Rejections answer 429 with a
/// Retry-After hint.
/// </summary>
internal sealed class AuthConfigRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <summary>The requests per minute one address may make.</summary>
    public const int RequestsPerMinute = 30;

    private readonly ILogger<AuthConfigRateLimiterPolicy> _logger;

    /// <summary>Initializes a new instance of the <see cref="AuthConfigRateLimiterPolicy"/> class.</summary>
    /// <param name="logger">The logger.</param>
    public AuthConfigRateLimiterPolicy(ILogger<AuthConfigRateLimiterPolicy> logger)
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
            "auth-config:" + RateLimitPartitionKey.ForAddress(httpContext),
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
