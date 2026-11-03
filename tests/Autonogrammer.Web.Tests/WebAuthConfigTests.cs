using System.Globalization;
using System.Net;
using System.Text;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.UI.Components.Auth;
using Autonogrammer.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>The built auth origin is refused when it does not match, and a 429 waits 60 seconds without reading Retry-After.</summary>
public sealed class WebAuthConfigTests
{
    /// <summary>A project URL on another origin is refused before any sign-in call.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AMismatchedProjectOriginIsRefused()
    {
        using var routes = new RoutedHandler();
        routes.OnJson(HttpMethod.Get, "/auth/config", """{"mode":"oidc","authority":null,"audience":null,"projectUrl":"https://other.example","publishableKey":"sb_publishable_test"}""");
        using var http = new HttpClient(routes, disposeHandler: false) { BaseAddress = new Uri("http://api.invalid/") };

        var config = await new AuthConfigClient(http, "https://project.example").LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        config.ShouldBeNull();
        WebAuthOrigin.Refuses(
            new AuthConfig(AuthConfig.OidcMode, null, null, new Uri("https://other.example"), "sb_publishable_test"),
            "https://project.example").ShouldBeTrue();
    }

    /// <summary>A client block with no built origin is refused.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AClientBlockWithNoBuiltOriginIsRefused()
    {
        using var routes = new RoutedHandler();
        routes.OnJson(HttpMethod.Get, "/auth/config", """{"mode":"oidc","authority":null,"audience":null,"projectUrl":"https://project.example","publishableKey":"sb_publishable_test"}""");
        using var http = new HttpClient(routes, disposeHandler: false) { BaseAddress = new Uri("http://api.invalid/") };

        var config = await new AuthConfigClient(http).LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        config.ShouldBeNull();
    }

    /// <summary>A 429 surfaces after 60 seconds even when Retry-After says 1.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ARateLimitWaitsSixtySecondsWithoutReadingRetryAfter()
    {
        var clock = new FixedClock(DateTimeOffset.Parse("2026-10-30T12:00:00Z", CultureInfo.InvariantCulture));
        var services = new ServiceCollection().BuildServiceProvider();
        using var limit = new RateLimitHandler();
        using var session = new WebAccountSession(clock, services, authHandler: limit);
        session.Apply(ClientConfig(), "https://project.example", "https://web.example/");

        await session.SendCodeAsync("person@example.com", TestContext.Current.CancellationToken).ConfigureAwait(true);

        session.State.Error.ShouldBe(AuthErrorKind.TooManyRequests);
        session.RateLimitedUntil.ShouldBe(clock.GetUtcNow().Add(TimeSpan.FromSeconds(60)));
    }

    /// <summary>Devices stays closed when the account read fails, and opens only when dispatch is listed.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task DevicesStayClosedUnlessDispatchIsListed()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        using var closed = new WebAccountSession(TimeProvider.System, services, readAccount: _ => throw new HttpRequestException("unavailable"));
        closed.Apply(ClientConfig(), "https://project.example", "https://web.example/");
        await closed.RefreshDevicesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        closed.State.DevicesAvailable.ShouldBeFalse();

        using var sessionHandler = new SessionHandler();
        using var open = new WebAccountSession(
            TimeProvider.System,
            services,
            readAccount: _ => Task.FromResult<AccountResponse?>(new AccountResponse("sub", "person@example.com", null, null, ["dispatch"])),
            authHandler: sessionHandler);
        open.Apply(ClientConfig(), "https://project.example", "https://web.example/");
        await SignInAsync(open).ConfigureAwait(true);
        open.State.DevicesAvailable.ShouldBeTrue();
    }

    private static AuthConfig ClientConfig() => new(
        AuthConfig.OidcMode,
        null,
        null,
        new Uri("https://project.example"),
        "sb_publishable_test",
        new AuthMethods(true, false, [], false));

    private static async Task SignInAsync(WebAccountSession session)
    {
        await session.SendCodeAsync("person@example.com", TestContext.Current.CancellationToken).ConfigureAwait(true);
        await session.SubmitCodeAsync("123456", TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        internal FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class RateLimitHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("""{"error_code":"over_request_rate_limit"}""", Encoding.UTF8, "application/json"),
            };
            response.Headers.TryAddWithoutValidation("Retry-After", "1");
            return Task.FromResult(response);
        }
    }

    private sealed class SessionHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var body = path.Contains("verify", StringComparison.Ordinal)
                ? """{"access_token":"access-token-value","refresh_token":"refresh-token-value","expires_in":3600,"user":{"id":"user-1","email":"person@example.com"}}"""
                : "{}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
