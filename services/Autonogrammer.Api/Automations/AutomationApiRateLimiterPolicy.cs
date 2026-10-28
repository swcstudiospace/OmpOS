using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Autonogrammer.Api.Security;

namespace Autonogrammer.Api.Automations;

/// <summary>Token-bucket limiter of the automation management routes: one global bucket of its own. Rejections answer 429 with a Retry-After hint.</summary>
internal sealed class AutomationApiRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <summary>The name the management routes require.</summary>
    public const string PolicyName = "automations";

    private readonly IOptions<AutomationOptions> _options;
    private readonly ILogger<AutomationApiRateLimiterPolicy> _logger;

    /// <summary>Initializes a new instance of the <see cref="AutomationApiRateLimiterPolicy"/> class.</summary>
    /// <param name="options">The automation options carrying the per-minute budget.</param>
    /// <param name="logger">The logger.</param>
    public AutomationApiRateLimiterPolicy(IOptions<AutomationOptions> options, ILogger<AutomationApiRateLimiterPolicy> logger)
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
        return AutomationRateLimits.Bucket("automations", _options.Value.RequestsPerMinute);
    }
}
