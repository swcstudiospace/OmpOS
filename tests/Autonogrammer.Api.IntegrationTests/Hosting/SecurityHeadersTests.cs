using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Hosting;

/// <summary>Pins HSTS and the response security headers.</summary>
public sealed class SecurityHeadersTests
{
    private static void MapStubs(WebApplication app)
    {
        _ = app.MapGet("/json", static () => Results.Json(new { ok = true }));
        _ = app.MapGet("/problem", static () => Results.Problem(statusCode: 409, title: "Conflict."));
        _ = app.MapGet("/html", static () => Results.Content("<p>reference</p>", "text/html"));
        _ = app.MapGet("/cached", static (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "public, max-age=60";
            return Results.Json(new { ok = true });
        });
    }

    private static async Task<HttpResponseMessage> GetAsync(WebApplication app, string path, CancellationToken testToken, string? scheme = null, bool authorization = false)
    {
        var client = app.GetTestClient();
        if (scheme is not null)
        {
            client.BaseAddress = new Uri($"{scheme}://api.example.test");
        }

        using (client)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.RelativeOrAbsolute));
            if (authorization)
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer placeholder");
            }

            return await client.SendAsync(request, testToken).ConfigureAwait(false);
        }
    }

    private static void AssertBaseline(HttpResponseMessage response)
    {
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        var permissions = response.Headers.GetValues("Permissions-Policy").Single();
        permissions.ShouldContain("camera=()");
        permissions.ShouldContain("microphone=()");
        permissions.ShouldContain("geolocation=()");
    }

    /// <summary>Every response carries the baseline headers and the strict content-security policy: JSON, ProblemDetails and an unrouted 404.</summary>
    [Theory]
    [InlineData("/json", HttpStatusCode.OK)]
    [InlineData("/problem", HttpStatusCode.Conflict)]
    [InlineData("/missing", HttpStatusCode.NotFound)]
    public async Task Responses_CarryTheSecurityHeaders(string path, HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await HostingTestHost.StartAsync(ct, mapEndpoints: MapStubs).ConfigureAwait(true);

        using var response = await GetAsync(app, path, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(status);
        AssertBaseline(response);
        response.Headers.GetValues("Content-Security-Policy").ShouldBe(["default-src 'none'; frame-ancestors 'none'"]);
    }

    /// <summary>An HTML response (the Development API reference UI) keeps the baseline headers but gets no content-security policy.</summary>
    [Fact]
    public async Task HtmlResponse_HasNoContentSecurityPolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await HostingTestHost.StartAsync(ct, environment: "Development", mapEndpoints: MapStubs).ConfigureAwait(true);

        using var response = await GetAsync(app, "/html", ct).ConfigureAwait(true);

        AssertBaseline(response);
        response.Headers.Contains("Content-Security-Policy").ShouldBeFalse();
    }

    /// <summary>Authenticated responses that set no cache header get no-store; one that sets its own keeps it.</summary>
    [Fact]
    public async Task AuthenticatedResponses_GetNoStoreUnlessTheyChooseCaching()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await HostingTestHost.StartAsync(ct, mapEndpoints: MapStubs).ConfigureAwait(true);

        using var plain = await GetAsync(app, "/json", ct, authorization: true).ConfigureAwait(true);
        using var cached = await GetAsync(app, "/cached", ct, authorization: true).ConfigureAwait(true);
        using var anonymous = await GetAsync(app, "/json", ct).ConfigureAwait(true);

        plain.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        cached.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeFalse();
        anonymous.Headers.CacheControl.ShouldBeNull();
    }

    /// <summary>The security headers can be switched off.</summary>
    [Fact]
    public async Task Disabled_AddsNoHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var config = new Dictionary<string, string?> { ["Hosting:SecurityHeaders:Enabled"] = "false" };
        await using var app = await HostingTestHost.StartAsync(ct, config: config, mapEndpoints: MapStubs).ConfigureAwait(true);

        using var response = await GetAsync(app, "/json", ct).ConfigureAwait(true);

        response.Headers.Contains("X-Content-Type-Options").ShouldBeFalse();
    }

    /// <summary>HSTS is sent over HTTPS outside Development, with the configured max-age.</summary>
    [Fact]
    public async Task Hsts_IsSentOverHttpsInProduction()
    {
        var ct = TestContext.Current.CancellationToken;
        var config = new Dictionary<string, string?> { ["Hosting:Hsts:MaxAgeDays"] = "30" };
        await using var app = await HostingTestHost.StartAsync(ct, config: config, mapEndpoints: MapStubs).ConfigureAwait(true);

        using var response = await GetAsync(app, "/json", ct, scheme: "https").ConfigureAwait(true);

        response.Headers.GetValues("Strict-Transport-Security").Single().ShouldStartWith("max-age=2592000");
    }

    /// <summary>HSTS is never sent over plain HTTP.</summary>
    [Fact]
    public async Task Hsts_IsNotSentOverHttp()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await HostingTestHost.StartAsync(ct, mapEndpoints: MapStubs).ConfigureAwait(true);

        using var response = await GetAsync(app, "/json", ct, scheme: "http").ConfigureAwait(true);

        response.Headers.Contains("Strict-Transport-Security").ShouldBeFalse();
    }

    /// <summary>HSTS is not sent in Development, even over HTTPS.</summary>
    [Fact]
    public async Task Hsts_IsNotSentInDevelopment()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await HostingTestHost.StartAsync(ct, environment: "Development", mapEndpoints: MapStubs).ConfigureAwait(true);

        using var response = await GetAsync(app, "/json", ct, scheme: "https").ConfigureAwait(true);

        response.Headers.Contains("Strict-Transport-Security").ShouldBeFalse();
    }

    /// <summary>HSTS can be disabled outside Development.</summary>
    [Fact]
    public async Task Hsts_CanBeDisabled()
    {
        var ct = TestContext.Current.CancellationToken;
        var config = new Dictionary<string, string?> { ["Hosting:Hsts:Enabled"] = "false" };
        await using var app = await HostingTestHost.StartAsync(ct, config: config, mapEndpoints: MapStubs).ConfigureAwait(true);

        using var response = await GetAsync(app, "/json", ct, scheme: "https").ConfigureAwait(true);

        response.Headers.Contains("Strict-Transport-Security").ShouldBeFalse();
    }
}
