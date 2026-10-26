using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Api.Endpoints;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// API-01 rate limiting: the configured <c>Api:RateLimit:PromptsPerMinute</c> budget is enforced at the prompt endpoint. Once the
/// budget is spent, the next prompt answers 429 with a Retry-After hint. The prompt policy is resolved from keyed DI, so a host can
/// substitute it.
/// </summary>
public sealed class RateLimitPolicyTests
{
    /// <summary>Verifies the prompt endpoint answers 429 once the configured PromptsPerMinute budget is exceeded.</summary>
    [Fact]
    public async Task Prompt_BeyondConfiguredPromptsPerMinute_Returns429()
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:RateLimit:PromptsPerMinute"] = "2" };
        using var factory = new OmpApiFactory(config);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var first = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"one\"}", testToken).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // The second prompt is still inside the budget, so the limiter admits it. The session may already be busy with the first run.
        using var second = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"two\"}", testToken).ConfigureAwait(true);
        second.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);

        using var third = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"three\"}", testToken).ConfigureAwait(true);
        third.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        third.Headers.Contains("Retry-After").ShouldBeTrue("a rate-limited prompt carries a Retry-After hint");
    }

    /// <summary>
    /// Verifies a substituted prompt policy governs the prompt endpoint. The default budget is left at 60, so the second prompt answering
    /// 429 can only come from the substitute, which admits one prompt.
    /// </summary>
    [Fact]
    public async Task SubstitutedPromptPolicy_GovernsThePromptEndpoint()
    {
        using var factory = new OmpApiFactory(configureServices: static services =>
            services.AddKeyedSingleton<IRateLimiterPolicy<string>>(SessionEndpoints.PromptsPolicy, new SingleAdmissionPolicy()));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var first = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"one\"}", testToken).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        using var second = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"two\"}", testToken).ConfigureAwait(true);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>A substitute prompt policy that admits exactly one prompt and never replenishes.</summary>
    private sealed class SingleAdmissionPolicy : IRateLimiterPolicy<string>
    {
        /// <inheritdoc />
        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

        /// <inheritdoc />
        public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            return RateLimitPartition.GetTokenBucketLimiter("single-admission", static _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 1,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                ReplenishmentPeriod = TimeSpan.FromHours(1),
                TokensPerPeriod = 1,
                AutoReplenishment = false,
            });
        }
    }
}
