using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OmpOS.Api.Security;

namespace OmpOS.Api.Services.Providers;

/// <summary>
/// Token-bucket limiter for the provider endpoints: one global bucket of <see cref="ProvidersOptions.RequestsPerMinute"/> requests a
/// minute, like the session policies. Rejections answer 429 with a Retry-After hint and log the method and path, never a body.
/// </summary>
internal sealed class ProviderRateLimiterPolicy : IRateLimiterPolicy<string>
{
    private readonly IOptions<ProvidersOptions> _options;
    private readonly ILogger<ProviderRateLimiterPolicy> _logger;

    /// <summary>Initializes a new instance of the <see cref="ProviderRateLimiterPolicy"/> class.</summary>
    /// <param name="options">The provider options carrying the per-minute budget.</param>
    /// <param name="logger">The logger.</param>
    public ProviderRateLimiterPolicy(IOptions<ProvidersOptions> options, ILogger<ProviderRateLimiterPolicy> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => RateLimitRejection.Handler(_logger);

    /// <inheritdoc />
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var limit = _options.Value.RequestsPerMinute;
        return RateLimitPartition.GetTokenBucketLimiter(
            "providers",
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
