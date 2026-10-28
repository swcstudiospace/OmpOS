using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Autonogrammer.Api.Options;

namespace Autonogrammer.Api.Security;

/// <summary>
/// Token-bucket limiter for session creation. One global bucket for the same reason as
/// <see cref="SessionRateLimiterPolicy"/>: there is no auth identity to partition on, and the
/// creation route has no id to key by. Rejections answer 429 with a Retry-After hint.
/// </summary>
internal sealed class SessionCreationRateLimiterPolicy : IRateLimiterPolicy<string>
{
    private readonly IOptions<ApiOptions> _options;
    private readonly ILogger<SessionCreationRateLimiterPolicy> _logger;

    /// <summary>Initializes a new instance of the <see cref="SessionCreationRateLimiterPolicy"/> class.</summary>
    /// <param name="options">The API options carrying the per-minute budget.</param>
    /// <param name="logger">The logger.</param>
    public SessionCreationRateLimiterPolicy(IOptions<ApiOptions> options, ILogger<SessionCreationRateLimiterPolicy> logger)
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
        var limit = _options.Value.RateLimit.SessionsPerMinute;
        return RateLimitPartition.GetTokenBucketLimiter(
            "sessions",
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
