using System.Threading.RateLimiting;
using Autonogrammer.Api.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Services.WorkspaceSettings;

/// <summary>
/// Token-bucket limiter for the settings and workspace reads. It is one global bucket, like the prompt limiter: the limiter runs before authorization,
/// so no caller identity exists yet to partition on. The bucket is its own, so a settings page that refreshes cannot use up the prompt budget.
/// Rejections answer 429 with a Retry-After hint.
/// </summary>
internal sealed class WorkspaceSettingsRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <summary>Gets the name the endpoints require.</summary>
    public const string PolicyName = "workspace-settings";

    private readonly IOptions<WorkspaceSettingsOptions> _options;
    private readonly ILogger<WorkspaceSettingsRateLimiterPolicy> _logger;

    /// <summary>Initializes a new instance of the <see cref="WorkspaceSettingsRateLimiterPolicy"/> class.</summary>
    /// <param name="options">The options carrying the per-minute budget.</param>
    /// <param name="logger">The logger.</param>
    public WorkspaceSettingsRateLimiterPolicy(IOptions<WorkspaceSettingsOptions> options, ILogger<WorkspaceSettingsRateLimiterPolicy> logger)
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
            PolicyName,
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
