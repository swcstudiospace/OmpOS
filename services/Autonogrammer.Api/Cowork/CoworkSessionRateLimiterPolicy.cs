using System.Threading.RateLimiting;
using Autonogrammer.Api.Automations;
using Autonogrammer.Api.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cowork;

/// <summary>
/// Token-bucket limiter of the Cowork summary routes: one bucket per caller (<see cref="RateLimitPartitionKey"/>).
/// Rejections answer 429 with a Retry-After hint.
/// </summary>
internal sealed class CoworkSessionRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <summary>The name the summary routes require.</summary>
    public const string PolicyName = "cowork-sessions";

    private readonly IOptions<CoworkOptions> _options;
    private readonly ILogger<CoworkSessionRateLimiterPolicy> _logger;

    /// <summary>Initializes a new instance of the <see cref="CoworkSessionRateLimiterPolicy"/> class.</summary>
    /// <param name="options">The Cowork options carrying the per-minute budget.</param>
    /// <param name="logger">The logger.</param>
    public CoworkSessionRateLimiterPolicy(IOptions<CoworkOptions> options, ILogger<CoworkSessionRateLimiterPolicy> logger)
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
        return AutomationRateLimits.Bucket("cowork-sessions:" + RateLimitPartitionKey.For(httpContext), _options.Value.RequestsPerMinute);
    }
}
