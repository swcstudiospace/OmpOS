using System.Threading.RateLimiting;
using Autonogrammer.Api.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Accounts;

/// <summary>
/// Token-bucket limiter of the account routes: one bucket per caller (<see cref="RateLimitPartitionKey"/>) of its own.
/// Rejections answer 429 with a Retry-After hint.
/// </summary>
internal sealed class AccountsRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <summary>The name the account routes require.</summary>
    public const string PolicyName = "accounts";

    private readonly IOptions<AccountsOptions> _options;
    private readonly ILogger<AccountsRateLimiterPolicy> _logger;

    /// <summary>Initializes a new instance of the <see cref="AccountsRateLimiterPolicy"/> class.</summary>
    /// <param name="options">The account options carrying the per-minute budget.</param>
    /// <param name="logger">The logger.</param>
    public AccountsRateLimiterPolicy(IOptions<AccountsOptions> options, ILogger<AccountsRateLimiterPolicy> logger)
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
        var limit = _options.Value.RequestsPerMinute;
        return RateLimitPartition.GetTokenBucketLimiter(
            "accounts:" + RateLimitPartitionKey.For(httpContext),
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
