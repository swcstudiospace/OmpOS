using System.Net;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Autonogrammer.Api.Hosting;
using Autonogrammer.Api.Sse;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Hosting;

/// <summary>
/// Pins request timeouts: a slow route answers 504 with ProblemDetails, and streaming routes are exempt. The default policy is
/// shortened to 100 ms so the test needs one real, short timeout.
/// </summary>
public sealed class RequestTimeoutTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PastTimeout = TimeSpan.FromMilliseconds(500);

    private static void ShortenDefaultPolicy(IServiceCollection services) =>
        services.PostConfigure<RequestTimeoutOptions>(static options =>
            options.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TestTimeout,
                TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
                WriteTimeoutResponse = ConfigureRequestTimeoutOptions.WriteTimeoutAsync,
            });

    private static void MapStubs(WebApplication app)
    {
        _ = app.MapGet("/slow", static async (HttpContext context) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted).ConfigureAwait(false);
            return Results.Ok();
        });
        _ = app.MapGet("/stream", static () => new SseStreamResult(SlowStream()));
    }

    private static async IAsyncEnumerable<SseItem<string>> SlowStream([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new SseItem<string>("first", "message") { EventId = "1" };
        await Task.Delay(PastTimeout, cancellationToken).ConfigureAwait(false);
        yield return new SseItem<string>("second", "message") { EventId = "2" };
    }

    /// <summary>A route that outlives the default timeout answers 504 with a ProblemDetails body.</summary>
    [Fact]
    public async Task SlowRoute_Answers504WithProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await HostingTestHost.StartAsync(ct, configureServices: ShortenDefaultPolicy, mapEndpoints: MapStubs).ConfigureAwait(true);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(new Uri("/slow", UriKind.Relative), ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        body.ShouldContain("\"status\":504");
    }

    /// <summary>A server-sent event stream is exempt: it keeps delivering after the default timeout has passed.</summary>
    [Fact]
    public async Task StreamingRoute_IsExemptFromTheTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await HostingTestHost.StartAsync(ct, configureServices: ShortenDefaultPolicy, mapEndpoints: MapStubs).ConfigureAwait(true);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(new Uri("/stream", UriKind.Relative), ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        body.ShouldContain("data: first");
        body.ShouldContain("data: second");
    }

    /// <summary>A route that opts into the named NoTimeout policy outlives the default timeout.</summary>
    [Fact]
    public async Task NoTimeoutPolicy_ExemptsARoute()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await HostingTestHost.StartAsync(
            ct,
            configureServices: ShortenDefaultPolicy,
            mapEndpoints: static application => application.MapGet("/exempt", static async (HttpContext context) =>
            {
                await Task.Delay(PastTimeout, context.RequestAborted).ConfigureAwait(false);
                return Results.Ok();
            }).WithRequestTimeout(HostingPolicies.NoTimeout)).ConfigureAwait(true);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(new Uri("/exempt", UriKind.Relative), ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
