using System.Threading.RateLimiting;
using Autonogrammer.Api.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>Address-partitioned limiter for the anonymous render route.</summary>
internal sealed class ArtifactRenderRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <summary>The policy name. The render route uses this partition, not the owner partition.</summary>
    public const string PolicyName = "artifact-render";

    /// <inheritdoc />
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } = static (context, _) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return ValueTask.CompletedTask;
    };

    /// <inheritdoc />
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return RateLimitPartition.GetTokenBucketLimiter(
            "artifact-render:" + RateLimitPartitionKey.ForAddress(httpContext),
            static _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 120,
                TokensPerPeriod = 120,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    }
}
