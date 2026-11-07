using System.Net;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.UI.Components.Auth;
using Autonogrammer.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>
/// The bearer handler is the one place a 401 ends a session: once, without retrying and without looping. It sends the token only while one is held,
/// it ends the session only for the token that was refused, and a cancelled request ends nothing. The account-session tests run the same handler
/// against the real sign-in state.
/// </summary>
public sealed class BearerTokenUnauthorizedTests
{
    private const string Token = "owner-token-value-123";

    /// <summary>The bearer header is sent while a token is held, and not before or after.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task TheBearerIsSentOnlyWhileATokenIsHeld()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new FakeTokenSession();
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

    /// <summary>A 401 to a request that carried the held token ends the session, and the error never shows the token.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnUnauthorizedAnswerEndsTheSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new FakeTokenSession();
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

    /// <summary>A late 401 for a request sent with an old token leaves the newer token and its session intact.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ALateRefusalOfAnOldTokenDoesNotEndANewerSession()
    {
        const string NewerToken = "newer-owner-token-456";
        var ct = TestContext.Current.CancellationToken;
        var session = new FakeTokenSession();
        session.SetToken(Token);
        using var routes = new RoutedHandler();
        routes.OnProblem(HttpMethod.Get, "/account/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
        using var gate = new GatedResponseHandler(routes);
        using var handler = HandlerOver(session, gate);
        using var http = HttpOver(handler);

        var pending = Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(http).GetBoardAsync(ct));
        await gate.WaitForResponseAsync(ct).ConfigureAwait(true);
        session.SetToken(NewerToken);
        gate.ReleaseResponse();
        _ = await pending.ConfigureAwait(true);

        routes.Headers[0]["Authorization"].ShouldBe("Bearer " + Token);
        session.HasToken.ShouldBeTrue();
        session.SessionEnded.ShouldBeFalse();
        (await session.GetTokenAsync(ct).ConfigureAwait(true)).ShouldBe(NewerToken);
    }

    /// <summary>A held 401 that is cancelled is disposed without ending the session, and the cancelled request is not retried.</summary>
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
        var session = new FakeTokenSession();
        session.SetToken(Token);
        using var handler = HandlerOver(session, gate);
        using var http = HttpOver(handler);

        var pending = Should.ThrowAsync<OperationCanceledException>(() => new OmpApiClient(http).GetBoardAsync(cancellation.Token));
        await gate.WaitForResponseAsync(ct).ConfigureAwait(true);
        body.CanRead.ShouldBeTrue("the transport still owns the held response before cancellation");
        await cancellation.CancelAsync().ConfigureAwait(true);
        _ = await pending.ConfigureAwait(true);

        body.CanRead.ShouldBeFalse("the response must be disposed before its unsuccessful transfer completes");
        session.HasToken.ShouldBeTrue();
        session.SessionEnded.ShouldBeFalse();
        routes.Requests.Count.ShouldBe(1, "the cancelled request must not be retried");

        gate.ReleaseResponse();
        await SendAsync(http, ct).ConfigureAwait(true);

        routes.Headers[1]["Authorization"].ShouldBe("Bearer " + Token);
        routes.Requests.Count.ShouldBe(2, "the next request is sent once with the surviving token");
    }

    /// <summary>Several requests refused together end the session once, and no request is retried.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AStormOfUnauthorizedAnswersEndsTheSessionOnceWithoutLooping()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new FakeTokenSession();
        session.SetToken(Token);
        var changes = 0;
        session.Changed += (_, _) => changes++;
        using var routes = new RoutedHandler();
        routes.OnProblem(HttpMethod.Get, "/account/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
        using var handler = HandlerOver(session, routes);
        using var http = HttpOver(handler);
        var api = new OmpApiClient(http);

        var failures = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Should.ThrowAsync<ApiProblemException>(() => api.GetBoardAsync(ct)))).ConfigureAwait(true);

        failures.ShouldAllBe(static failure => failure.Kind == ApiProblemKind.Unauthorized);
        session.SessionEnded.ShouldBeTrue();
        changes.ShouldBe(1, "the session ends once, however many refusals arrive");
        routes.Requests.Count.ShouldBe(5, "each request is sent once and none is retried");
    }

    /// <summary>A 401 to a request that carried no token changes nothing: there is no session to end.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnUnauthorizedAnswerWithoutATokenChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new FakeTokenSession();
        var changes = 0;
        session.Changed += (_, _) => changes++;
        using var routes = new RoutedHandler();
        routes.OnProblem(HttpMethod.Get, "/account/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
        using var handler = HandlerOver(session, routes);
        using var http = HttpOver(handler);

        _ = await Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(http).GetBoardAsync(ct)).ConfigureAwait(true);

        routes.Headers[0].ContainsKey("Authorization").ShouldBeFalse();
        session.HasToken.ShouldBeFalse();
        session.SessionEnded.ShouldBeFalse();
        changes.ShouldBe(0);
    }

    /// <summary>A failure that is not a 401 keeps the token: only a refusal of the token ends a session.</summary>
    /// <param name="status">The status the API answered.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task OtherFailuresKeepTheToken(HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new FakeTokenSession();
        session.SetToken(Token);
        using var routes = new RoutedHandler();
        routes.OnProblem(HttpMethod.Get, "/account/board", status, null, "Failed.", "Try later.");
        using var handler = HandlerOver(session, routes);
        using var http = HttpOver(handler);

        _ = await Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(http).GetBoardAsync(ct)).ConfigureAwait(true);

        session.HasToken.ShouldBeTrue();
        session.SessionEnded.ShouldBeFalse();
        (await session.GetTokenAsync(ct).ConfigureAwait(true)).ShouldBe(Token);
    }

    /// <summary>An unauthorized answer to a bearer request ends the account session, and the request carried the account's access token.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnUnauthorizedAnswerExpiresTheAccountSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection().BuildServiceProvider();
        using var authHandler = new AccountTokenHandler("access-token-value");
        using var account = new WebAccountSession(TimeProvider.System, services, authHandler: authHandler);
        account.Apply(OidcConfig(), "https://project.example", "https://web.example/");
        await SignInAsync(account, "123456", ct).ConfigureAwait(true);
        account.HasToken.ShouldBeTrue();

        using var routes = new RoutedHandler();
        routes.OnProblem(HttpMethod.Get, "/account/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
        using var handler = new BearerTokenHandler(account, routes, account.Expire);
        using var http = HttpOver(handler);

        _ = await Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(http).GetBoardAsync(ct)).ConfigureAwait(true);

        account.HasToken.ShouldBeFalse();
        account.SessionEnded.ShouldBeTrue();
        account.State.Error.ShouldBe(AuthErrorKind.AccountRejected);
        routes.Headers[0]["Authorization"].ShouldBe("Bearer access-token-value");
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
        var services = new ServiceCollection().BuildServiceProvider();
        using var authHandler = new AccountTokenHandler("access-token-value-1", "access-token-value-2");
        using var account = new WebAccountSession(TimeProvider.System, services, authHandler: authHandler);
        account.Apply(OidcConfig(), "https://project.example", "https://web.example/");

        await SignInAsync(account, "111111", ct).ConfigureAwait(true);
        var firstToken = await account.GetTokenAsync(ct).ConfigureAwait(true);
        await account.SignOutAsync(everywhere: false, ct).ConfigureAwait(true);
        await SignInAsync(account, "222222", ct).ConfigureAwait(true);

        account.Expire(firstToken!);

        account.HasToken.ShouldBeTrue("a refusal for an earlier sign-in must not sign out the current one");
        account.SessionEnded.ShouldBeFalse();
        var secondToken = await account.GetTokenAsync(ct).ConfigureAwait(true);
        secondToken.ShouldBe("access-token-value-2");

        account.Expire(secondToken!);

        account.HasToken.ShouldBeFalse();
        account.SessionEnded.ShouldBeTrue();
    }

    private static BearerTokenHandler HandlerOver(FakeTokenSession session, HttpMessageHandler inner) => new(session, inner, session.Expire);

    private static HttpClient HttpOver(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

    private static async Task SendAsync(HttpClient http, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri("workspace", UriKind.Relative), cancellationToken).ConfigureAwait(false);
    }

    private static AuthConfig OidcConfig() =>
        new(
            AuthConfig.OidcMode,
            null,
            null,
            new Uri("https://project.example"),
            "sb_publishable_test",
            new AuthMethods(EmailCode: true, MagicLink: false, Providers: [], Wallet: false));

    private static async Task SignInAsync(WebAccountSession account, string code, CancellationToken cancellationToken)
    {
        await account.SendCodeAsync("person@example.com", cancellationToken).ConfigureAwait(true);
        await account.SubmitCodeAsync(code, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>A token holder with the rules the production session applies to a refusal: it ends once, and only for the token it holds.</summary>
    private sealed class FakeTokenSession : IApiTokenProvider
    {
        private readonly Lock _gate = new();
        private string? _token;

        public event EventHandler? Changed;

        public bool HasToken
        {
            get
            {
                lock (_gate)
                {
                    return _token is not null;
                }
            }
        }

        public bool SessionEnded { get; private set; }

        public ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            lock (_gate)
            {
                return ValueTask.FromResult(_token);
            }
        }

        public void SetToken(string token)
        {
            bool raised;
            lock (_gate)
            {
                raised = _token is null || SessionEnded;
                _token = token;
                SessionEnded = false;
            }

            if (raised)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Clear()
        {
            bool raised;
            lock (_gate)
            {
                raised = _token is not null;
                _token = null;
                SessionEnded = false;
            }

            if (raised)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Expire(string refusedToken)
        {
            bool ended;
            lock (_gate)
            {
                ended = string.Equals(_token, refusedToken, StringComparison.Ordinal);
                if (ended)
                {
                    _token = null;
                    SessionEnded = true;
                }
            }

            if (ended)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
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
