using System.Net;
using Autonogrammer.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>The bearer handler sends the owner token only when one is held, and is the one place a 401 ends the session: once, without retrying and without looping.</summary>
public sealed class BearerTokenUnauthorizedTests
{
    private const string Token = "owner-token-value-123";

    private static BearerTokenHandler HandlerOver(OwnerTokenSession session, RoutedHandler routes) => new(session, routes, session.Expire);

    private static HttpClient HttpOver(BearerTokenHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

    private static async Task SendAsync(HttpClient http, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri("workspace", UriKind.Relative), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The bearer header is sent while a token is held, and not before or after.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task TheBearerIsSentOnlyWhileATokenIsHeld()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new OwnerTokenSession();
        using var routes = new RoutedHandler();
        routes.OnJson(HttpMethod.Get, "/workspace", """{"name":"w","pluginCheck":{}}""");
        using var handler = HandlerOver(session, routes);
        using var http = HttpOver(handler);

        await SendAsync(http, ct).ConfigureAwait(true);

        session.SetToken(Token);
        await SendAsync(http, ct).ConfigureAwait(true);

        session.Clear();
        await SendAsync(http, ct).ConfigureAwait(true);

        routes.Headers[0].ContainsKey("Authorization").ShouldBeFalse();
        routes.Headers[1]["Authorization"].ShouldBe("Bearer " + Token);
        routes.Headers[2].ContainsKey("Authorization").ShouldBeFalse();
    }

    /// <summary>A 401 to a request that carried the accepted token ends the session with a notice, and the next request carries no token.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnUnauthorizedAnswerEndsTheSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new OwnerTokenSession();
        session.SetToken(Token);
        using var routes = new RoutedHandler();
        routes.OnProblem(HttpMethod.Get, "/account/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
        using var handler = HandlerOver(session, routes);
        using var http = HttpOver(handler);

        var failure = await Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(http).GetBoardAsync(ct)).ConfigureAwait(true);

        failure.Kind.ShouldBe(ApiProblemKind.Unauthorized);
        failure.ToString().ShouldNotContain(Token);
        session.HasToken.ShouldBeFalse();
        session.SessionEnded.ShouldBeTrue();
        (await session.GetTokenAsync(ct).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>A 401 storm (several requests in flight when the token is refused) ends the session once and issues no extra request.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AStormOfUnauthorizedAnswersEndsTheSessionOnceWithoutLooping()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new OwnerTokenSession();
        session.SetToken(Token);
        var changes = 0;
        session.Changed += (_, _) => changes++;
        using var routes = new RoutedHandler();
        routes.OnProblem(HttpMethod.Get, "/account/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
        using var handler = HandlerOver(session, routes);
        using var http = HttpOver(handler);
        var api = new OmpApiClient(http);

        var calls = Enumerable.Range(0, 5).Select(_ => Should.ThrowAsync<ApiProblemException>(() => api.GetBoardAsync(ct))).ToList();
        await Task.WhenAll(calls).ConfigureAwait(true);

        changes.ShouldBe(1);
        routes.Requests.Count.ShouldBe(5, "every request is sent once and none is repeated");
        session.SessionEnded.ShouldBeTrue();
    }

    /// <summary>A 401 to a request that carried no token changes nothing: there is no session to end.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnUnauthorizedAnswerWithoutATokenChangesNothing()
    {
        var session = new OwnerTokenSession();
        var changes = 0;
        session.Changed += (_, _) => changes++;
        using var routes = new RoutedHandler();
        routes.OnProblem(HttpMethod.Get, "/account/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
        using var handler = HandlerOver(session, routes);
        using var http = HttpOver(handler);

        _ = await Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(http).GetBoardAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);

        changes.ShouldBe(0);
        session.SessionEnded.ShouldBeFalse();
    }

    /// <summary>Another failure (a 500, a 403) keeps the token: only a 401 means the token no longer works.</summary>
    /// <param name="status">The status.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task OtherFailuresKeepTheToken(HttpStatusCode status)
    {
        var session = new OwnerTokenSession();
        session.SetToken(Token);
        using var routes = new RoutedHandler();
        routes.OnProblem(HttpMethod.Get, "/account/board", status, null, "Failed.", "Try later.");
        using var handler = HandlerOver(session, routes);
        using var http = HttpOver(handler);

        _ = await Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(http).GetBoardAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);

        session.HasToken.ShouldBeTrue();
        session.SessionEnded.ShouldBeFalse();
    }

    /// <summary>With the owner-token session active, a 401 still calls OwnerTokenSession.Expire through the router.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnUnauthorizedAnswerStillExpiresTheOwnerTokenSession()
    {
        var owner = new OwnerTokenSession();
        owner.SetToken(Token);
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();
        using var account = new WebAccountSession(TimeProvider.System, services);
        var router = new WebAuthSessionRouter(owner, account);
        using var routes = new RoutedHandler();
        routes.OnProblem(HttpMethod.Get, "/account/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
        using var handler = new BearerTokenHandler(router, routes, router.Expire);
        using var http = HttpOver(handler);

        _ = await Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(http).GetBoardAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);

        owner.HasToken.ShouldBeFalse();
        owner.SessionEnded.ShouldBeTrue();
        router.SessionEnded.ShouldBeTrue();
    }

    /// <summary>A 401 to a request that carried the account token signs that tab out with AccountRejected.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnUnauthorizedAnswerExpiresTheAccountSession()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();
        using var authHandler = new AccountTokenHandler();
        using var account = new WebAccountSession(TimeProvider.System, services, authHandler: authHandler);
        account.Apply(
            new Autonogrammer.UI.Components.Auth.AuthConfig(
                Autonogrammer.UI.Components.Auth.AuthConfig.OidcMode,
                null,
                null,
                new Uri("https://project.example"),
                "sb_publishable_test",
                new Autonogrammer.Contracts.Accounts.AuthMethods(true, false)),
            "https://project.example",
            "https://web.example/");
        await account.SendCodeAsync("person@example.com", TestContext.Current.CancellationToken).ConfigureAwait(true);
        await account.SubmitCodeAsync("123456", TestContext.Current.CancellationToken).ConfigureAwait(true);
        account.HasToken.ShouldBeTrue();

        var owner = new OwnerTokenSession();
        var router = new WebAuthSessionRouter(owner, account);
        router.Apply(
            new Autonogrammer.UI.Components.Auth.AuthConfig(
                Autonogrammer.UI.Components.Auth.AuthConfig.OidcMode,
                null,
                null,
                new Uri("https://project.example"),
                "sb_publishable_test",
                new Autonogrammer.Contracts.Accounts.AuthMethods(true, false)),
            "https://project.example",
            "https://web.example/");
        using var routes = new RoutedHandler();
        routes.OnProblem(HttpMethod.Get, "/account/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
        using var handler = new BearerTokenHandler(router, routes, router.Expire);
        using var http = HttpOver(handler);

        _ = await Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(http).GetBoardAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);

        account.HasToken.ShouldBeFalse();
        account.SessionEnded.ShouldBeTrue();
        account.State.Error.ShouldBe(Autonogrammer.Contracts.Accounts.AuthErrorKind.AccountRejected);
        owner.SessionEnded.ShouldBeFalse();
    }

    private sealed class AccountTokenHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var body = path.Contains("verify", StringComparison.Ordinal)
                ? """{"access_token":"access-token-value","refresh_token":"refresh-token-value","expires_in":3600,"user":{"id":"user-1","email":"person@example.com"}}"""
                : "{}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

}
