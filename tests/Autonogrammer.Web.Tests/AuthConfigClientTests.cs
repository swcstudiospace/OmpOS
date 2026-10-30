using System.Net;
using Autonogrammer.Web.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>The anonymous read of <c>GET /auth/config</c>: each mode, a body that is not the documented shape, a failing API and an unreachable one.</summary>
public sealed class AuthConfigClientTests
{
    private static HttpClient HttpOver(RoutedHandler routes) =>
        new(routes, disposeHandler: false) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

    /// <summary>Each of the three modes is read with its authority and audience, camel-cased as the contract says.</summary>
    /// <param name="json">The body.</param>
    /// <param name="mode">The expected mode.</param>
    /// <param name="authority">The expected authority.</param>
    /// <param name="audience">The expected audience.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData("""{"mode":"owner-token","authority":null,"audience":null}""", "owner-token", null, null)]
    [InlineData("""{"mode":"oidc","authority":"https://issuer.example","audience":"autonogrammer"}""", "oidc", "https://issuer.example", "autonogrammer")]
    [InlineData("""{"mode":"development","authority":null,"audience":null}""", "development", null, null)]
    [InlineData("""{"mode":"owner-token"}""", "owner-token", null, null)]
    public async Task EachModeIsRead(string json, string mode, string? authority, string? audience)
    {
        using var routes = new RoutedHandler();
        routes.OnJson(HttpMethod.Get, "/auth/config", json);

        using var http = HttpOver(routes);
        var config = await new AuthConfigClient(http).LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        config.ShouldNotBeNull();
        config.Mode.ShouldBe(mode);
        config.Authority.ShouldBe(authority);
        config.Audience.ShouldBe(audience);
        config.IsKnownMode().ShouldBeTrue();
        routes.Requests.ShouldBe(["GET /auth/config"]);
        routes.Headers[0].ContainsKey("Authorization").ShouldBeFalse("the configuration is read anonymously");
    }

    /// <summary>A mode this client does not know is returned as it is, and flagged unknown for the gate to refuse.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnUnknownModeIsReturnedAndFlagged()
    {
        using var routes = new RoutedHandler();
        routes.OnJson(HttpMethod.Get, "/auth/config", """{"mode":"saml"}""");

        using var http = HttpOver(routes);
        var config = await new AuthConfigClient(http).LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        config.ShouldNotBeNull();
        config.IsKnownMode().ShouldBeFalse();
    }

    /// <summary>A body that is not JSON, not an object, empty, or without a mode is no configuration.</summary>
    /// <param name="body">The body.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"mode":""}""")]
    public async Task ABodyThatIsNotTheDocumentedShapeIsNoConfiguration(string body)
    {
        using var routes = new RoutedHandler();
        routes.OnJson(HttpMethod.Get, "/auth/config", body);

        using var http = HttpOver(routes);
        var config = await new AuthConfigClient(http).LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        config.ShouldBeNull();
    }

    /// <summary>A failing API (500) and a missing route (404) are no configuration.</summary>
    /// <param name="status">The status.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task AFailingApiIsNoConfiguration(HttpStatusCode status)
    {
        using var routes = new RoutedHandler();
        routes.OnJson(HttpMethod.Get, "/auth/config", """{"mode":"development"}""", status);

        using var http = HttpOver(routes);
        var config = await new AuthConfigClient(http).LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        config.ShouldBeNull();
    }

    /// <summary>An API that cannot be reached is no configuration, not an exception.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnUnreachableApiIsNoConfiguration()
    {
        using var routes = new RoutedHandler();
        routes.On(HttpMethod.Get, "/auth/config", _ => throw new HttpRequestException("offline"));

        using var http = HttpOver(routes);
        var config = await new AuthConfigClient(http).LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        config.ShouldBeNull();
    }

    /// <summary>An API that accepts the connection and then says nothing is no configuration after the deadline, so the page offers Retry instead of waiting for the browser's timeout.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ASilentApiIsNoConfigurationAtTheDeadline()
    {
        using var silent = new HangingHandler();
        using var http = new HttpClient(silent, disposeHandler: false) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

        var config = await new AuthConfigClient(http, TimeSpan.FromMilliseconds(50)).LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        config.ShouldBeNull();
    }

    /// <summary>A cancelled load is cancelled, not reported as an unreachable API.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ACancelledLoadIsCancelled()
    {
        using var routes = new RoutedHandler();
        routes.OnJson(HttpMethod.Get, "/auth/config", """{"mode":"development"}""");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        using var http = HttpOver(routes);
        await Should.ThrowAsync<OperationCanceledException>(() => new AuthConfigClient(http).LoadAsync(cancelled.Token)).ConfigureAwait(true);
    }
}
