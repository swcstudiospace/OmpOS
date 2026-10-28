using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OmpOS.Api.Security;

namespace OmpOS.Api.Automations;

/// <summary>
/// Token-bucket limiter of the public webhook receiver. It is one global bucket, like the other policies: the receiver is anonymous, and a
/// bucket per automation identifier would grow one limiter per identifier an attacker invents. The bucket is its own, so a burst of
/// deliveries cannot use up the prompt budget. Rejections answer 429 with a Retry-After hint.
/// </summary>
internal sealed class AutomationWebhookRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <summary>The name the receiver route requires.</summary>
    public const string PolicyName = "automation-webhooks";

    private readonly IOptions<AutomationOptions> _options;
    private readonly ILogger<AutomationWebhookRateLimiterPolicy> _logger;

    /// <summary>Initializes a new instance of the <see cref="AutomationWebhookRateLimiterPolicy"/> class.</summary>
    /// <param name="options">The automation options carrying the per-minute budget.</param>
    /// <param name="logger">The logger.</param>
    public AutomationWebhookRateLimiterPolicy(IOptions<AutomationOptions> options, ILogger<AutomationWebhookRateLimiterPolicy> logger)
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
        return AutomationRateLimits.Bucket("automation-webhooks", _options.Value.WebhookRequestsPerMinute);
    }
}
