using System.Threading.RateLimiting;
using Autonogrammer.Api.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Security;

/// <summary>
/// Token-bucket limiter for prompt submissions, one bucket per caller (<see cref="RateLimitPartitionKey"/>: the authenticated subject,
/// else the remote address) with the configured budget per key. The limiter runs after authentication, so the subject is known; the
/// unauthenticated session id route value is never a key. Idle buckets are disposed by the partitioned limiter, which bounds memory.
/// Rejections answer 429 with a Retry-After hint.
/// </summary>
internal sealed class SessionRateLimiterPolicy : IRateLimiterPolicy<string>
{
    private readonly IOptions<ApiOptions> _options;
    private readonly ILogger<SessionRateLimiterPolicy> _logger;

    /// <summary>Initializes a new instance of the <see cref="SessionRateLimiterPolicy"/> class.</summary>
    /// <param name="options">The API options carrying the per-minute budget.</param>
    /// <param name="logger">The logger.</param>
    public SessionRateLimiterPolicy(IOptions<ApiOptions> options, ILogger<SessionRateLimiterPolicy> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => RateLimitRejection.Handler(_logger);

    /// <inheritdoc />
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var limit = _options.Value.RateLimit.PromptsPerMinute;
        return RateLimitPartition.GetTokenBucketLimiter(
            "prompts:" + RateLimitPartitionKey.For(httpContext),
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
}
