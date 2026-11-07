using System.Threading.RateLimiting;
using Autonogrammer.Api.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>Per-caller limiter for owner-scoped artifact routes.</summary>
internal sealed class ArtifactOwnerRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <summary>The policy name.</summary>
    public const string PolicyName = "artifact-owner";

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
            "artifact-owner:" + RateLimitPartitionKey.For(httpContext),
            static _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 600,
                TokensPerPeriod = 600,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    }
}
