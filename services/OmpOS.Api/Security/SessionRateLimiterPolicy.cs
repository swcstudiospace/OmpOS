using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OmpOS.Api.Options;

namespace OmpOS.Api.Security;

/// <summary>
/// Token-bucket limiter for prompt submissions. Phase 2 serves a single dev principal with no
/// auth identity worth partitioning on, so this is one global bucket: keying limiter lifetime
/// on the unauthenticated session id route value would grow one limiter plus timer per id
/// forever. A per-session fairness upgrade is blocked on real auth.
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
            "prompts",
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
