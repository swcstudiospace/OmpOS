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

    /// <summary>The production registration preserves a newer owner token when an in-flight request with the old token receives a late 401.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ALateRefusalOfAnOldTokenDoesNotEndANewerSession()
    {
        const string NewerToken = "newer-owner-token-456";
        var ct = TestContext.Current.CancellationToken;
        using var routes = new RoutedHandler();
        routes.OnProblem(HttpMethod.Get, "/account/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
        routes.OnJson(HttpMethod.Get, "/workspace", """{"name":"w","pluginCheck":{}}""");
        using var gate = new GatedResponseHandler(routes);
        var services = new ServiceCollection();
        services.AddAutonogrammerWebServices(
            _ => gate,
            _ => new Uri("http://api.invalid/", UriKind.Absolute),
            _ => new WebWorkbenchOptions(IsDemo: false, ApiHost: "api.invalid"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<OwnerTokenSession>();
        var router = scope.ServiceProvider.GetRequiredService<WebAuthSessionRouter>();
        var api = scope.ServiceProvider.GetRequiredService<IOmpApiClient>();
        scope.ServiceProvider.GetRequiredService<IApiTokenProvider>().ShouldBeSameAs(router);
        session.SetToken(Token);

        // Hold the actual registered pipeline's response after the old bearer was sent, then accept the replacement before releasing its 401.
        var pending = Should.ThrowAsync<ApiProblemException>(() => api.GetBoardAsync(ct));
        await gate.WaitForResponseAsync(ct).ConfigureAwait(true);
        session.SetToken(NewerToken);
        var changes = 0;
        router.Changed += (_, _) => changes++;
        gate.ReleaseResponse();

        var failure = await pending.ConfigureAwait(true);

        failure.Kind.ShouldBe(ApiProblemKind.Unauthorized);
        failure.ToString().ShouldNotContain(Token);
        failure.ToString().ShouldNotContain(NewerToken);
        session.HasToken.ShouldBeTrue();
        session.SessionEnded.ShouldBeFalse();
        router.HasToken.ShouldBeTrue();
        router.SessionEnded.ShouldBeFalse();
        (await router.GetTokenAsync(ct).ConfigureAwait(true)).ShouldBe(NewerToken);
        changes.ShouldBe(0, "refusing the old token must not notify the shell that the new session ended");
        routes.Headers[0]["Authorization"].ShouldBe("Bearer " + Token);
        routes.Requests.Count.ShouldBe(1, "the refused request must not be retried");

        await SendAsync(scope.ServiceProvider.GetRequiredService<ApiHttp>().Client, ct).ConfigureAwait(true);

        routes.Headers[1]["Authorization"].ShouldBe("Bearer " + NewerToken);
        routes.Requests.Count.ShouldBe(2, "the next request is sent once with the surviving token");
        session.HasToken.ShouldBeTrue();
        session.SessionEnded.ShouldBeFalse();
    }

    /// <summary>Cancelling a held 401 in the registered pipeline disposes its response without ending the owner session or retrying the request.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task CancellingAHeldUnauthorizedAnswerDisposesItWithoutEndingTheSession()
    {
        var ct = TestContext.Current.CancellationToken;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var body = new MemoryStream();
        using var response = new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StreamContent(body) };
        using var routes = new RoutedHandler();
        routes.On(HttpMethod.Get, "/account/board", _ => response);
        routes.OnJson(HttpMethod.Get, "/workspace", """{"name":"w","pluginCheck":{}}""");
        using var gate = new GatedResponseHandler(routes);
        var services = new ServiceCollection();
        services.AddAutonogrammerWebServices(
            _ => gate,
            _ => new Uri("http://api.invalid/", UriKind.Absolute),
            _ => new WebWorkbenchOptions(IsDemo: false, ApiHost: "api.invalid"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<OwnerTokenSession>();
        var router = scope.ServiceProvider.GetRequiredService<WebAuthSessionRouter>();
        var api = scope.ServiceProvider.GetRequiredService<IOmpApiClient>();
        session.SetToken(Token);
        var changes = 0;
        router.Changed += (_, _) => changes++;

        var pending = Should.ThrowAsync<OperationCanceledException>(() => api.GetBoardAsync(cancellation.Token));
        await gate.WaitForResponseAsync(ct).ConfigureAwait(true);
        body.CanRead.ShouldBeTrue("the transport still owns the held response before cancellation");
        await cancellation.CancelAsync().ConfigureAwait(true);
        _ = await pending.ConfigureAwait(true);

        body.CanRead.ShouldBeFalse("the response must be disposed before its unsuccessful transfer completes");
        session.HasToken.ShouldBeTrue();
        session.SessionEnded.ShouldBeFalse();
        router.HasToken.ShouldBeTrue();
        router.SessionEnded.ShouldBeFalse();
        (await router.GetTokenAsync(ct).ConfigureAwait(true)).ShouldBe(Token);
        changes.ShouldBe(0, "cancellation must not be treated as an owner-token refusal");
        routes.Headers[0]["Authorization"].ShouldBe("Bearer " + Token);
        routes.Requests.Count.ShouldBe(1, "the cancelled request must not be retried");
        gate.ReleaseResponse();

        await SendAsync(scope.ServiceProvider.GetRequiredService<ApiHttp>().Client, ct).ConfigureAwait(true);

        routes.Headers[1]["Authorization"].ShouldBe("Bearer " + Token);
        routes.Requests.Count.ShouldBe(2, "the next request is sent once with the surviving token");
        changes.ShouldBe(0);
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

    /// <summary>The production registration expires the owner session once when a 401 refuses its current token, and the next request carries no token.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnUnauthorizedAnswerStillExpiresTheOwnerTokenSession()
    {
        var ct = TestContext.Current.CancellationToken;
        using var routes = new RoutedHandler();
        routes.OnProblem(HttpMethod.Get, "/account/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
        routes.OnJson(HttpMethod.Get, "/workspace", """{"name":"w","pluginCheck":{}}""");
        var services = new ServiceCollection();
        services.AddAutonogrammerWebServices(
            _ => routes,
            _ => new Uri("http://api.invalid/", UriKind.Absolute),
            _ => new WebWorkbenchOptions(IsDemo: false, ApiHost: "api.invalid"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var owner = scope.ServiceProvider.GetRequiredService<OwnerTokenSession>();
        var router = scope.ServiceProvider.GetRequiredService<WebAuthSessionRouter>();
        owner.SetToken(Token);
        var changes = 0;
        router.Changed += (_, _) => changes++;

        var failure = await Should.ThrowAsync<ApiProblemException>(() =>
            scope.ServiceProvider.GetRequiredService<IOmpApiClient>().GetBoardAsync(ct)).ConfigureAwait(true);

        failure.Kind.ShouldBe(ApiProblemKind.Unauthorized);
        routes.Headers[0]["Authorization"].ShouldBe("Bearer " + Token);

        owner.HasToken.ShouldBeFalse();
        owner.SessionEnded.ShouldBeTrue();
        router.SessionEnded.ShouldBeTrue();
        changes.ShouldBe(1);
        (await router.GetTokenAsync(ct).ConfigureAwait(true)).ShouldBeNull();

        await SendAsync(scope.ServiceProvider.GetRequiredService<ApiHttp>().Client, ct).ConfigureAwait(true);

        routes.Headers[1].ContainsKey("Authorization").ShouldBeFalse();
        routes.Requests.Count.ShouldBe(2, "neither the refusal nor the next request is retried");
        changes.ShouldBe(1);
    }

    /// <summary>A 401 to a request that carried the account token signs that tab out with AccountRejected.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnUnauthorizedAnswerExpiresTheAccountSession()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();
        using var authHandler = new AccountTokenHandler("access-token-value");
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
        owner.SetToken(Token);
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
        owner.HasToken.ShouldBeTrue();
        (await owner.GetTokenAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(Token);
        routes.Headers[0]["Authorization"].ShouldBe("Bearer access-token-value");
    }

    /// <summary>Holds a transport response until the test has replaced the token, without timers or a reconstructed bearer pipeline.</summary>
    private sealed class GatedResponseHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private readonly TaskCompletionSource _responseReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task WaitForResponseAsync(CancellationToken cancellationToken) => _responseReady.Task.WaitAsync(cancellationToken);

        internal void ReleaseResponse() => _releaseResponse.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            try
            {
                _responseReady.TrySetResult();
                await _releaseResponse.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return response;
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }
    }

    /// <summary>
    /// A refusal for the token of an earlier sign-in ends nothing. After a sign-out and a second sign-in, a late 401 for the first token leaves the second
    /// sign-in intact; only the token that second sign-in was handed ends it.
    /// </summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ALateRefusalForAnEarlierSignInDoesNotEndTheCurrentOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();
        using var authHandler = new AccountTokenHandler("access-token-value-1", "access-token-value-2");
        using var account = new WebAccountSession(TimeProvider.System, services, authHandler: authHandler);
        var owner = new OwnerTokenSession();
        var router = new WebAuthSessionRouter(owner, account);
        router.Apply(OidcConfig(), "https://project.example", "https://web.example/");

        await SignInAsync(account, "111111", ct).ConfigureAwait(true);
        var firstToken = await router.GetTokenAsync(ct).ConfigureAwait(true);
        await account.SignOutAsync(everywhere: false, ct).ConfigureAwait(true);
        await SignInAsync(account, "222222", ct).ConfigureAwait(true);

        router.Expire(firstToken!);

        account.HasToken.ShouldBeTrue("a refusal for an earlier sign-in must not sign out the current one");
        account.SessionEnded.ShouldBeFalse();
        var secondToken = await router.GetTokenAsync(ct).ConfigureAwait(true);
        secondToken.ShouldBe("access-token-value-2");

        router.Expire(secondToken!);

        account.HasToken.ShouldBeFalse();
        account.SessionEnded.ShouldBeTrue();
    }

    private static Autonogrammer.UI.Components.Auth.AuthConfig OidcConfig() =>
        new(
            Autonogrammer.UI.Components.Auth.AuthConfig.OidcMode,
            null,
            null,
            new Uri("https://project.example"),
            "sb_publishable_test",
            new Autonogrammer.Contracts.Accounts.AuthMethods(true, false));

    private static async Task SignInAsync(WebAccountSession account, string code, CancellationToken cancellationToken)
    {
        await account.SendCodeAsync("person@example.com", cancellationToken).ConfigureAwait(true);
        await account.SubmitCodeAsync(code, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Answers each sign-in verification with the next access token in its list, so a test can tell one sign-in from the next.</summary>
    private sealed class AccountTokenHandler(params string[] accessTokens) : HttpMessageHandler
    {
        private int _verifications;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var body = "{}";
            if (path.Contains("verify", StringComparison.Ordinal))
            {
                var accessToken = accessTokens[Math.Min(_verifications, accessTokens.Length - 1)];
                _verifications++;
                body = """{"access_token":"ACCESS_TOKEN","refresh_token":"refresh-token-value","expires_in":3600,"user":{"id":"user-1","email":"person@example.com"}}""".Replace("ACCESS_TOKEN", accessToken, StringComparison.Ordinal);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

}
