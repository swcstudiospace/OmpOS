using System.Net;
using Autonogrammer.Web.Services;
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
        routes.OnProblem(HttpMethod.Get, "/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
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
        routes.OnProblem(HttpMethod.Get, "/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
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
        routes.OnProblem(HttpMethod.Get, "/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
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
        routes.OnProblem(HttpMethod.Get, "/board", status, null, "Failed.", "Try later.");
        using var handler = HandlerOver(session, routes);
        using var http = HttpOver(handler);

        _ = await Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(http).GetBoardAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);

        session.HasToken.ShouldBeTrue();
        session.SessionEnded.ShouldBeFalse();
    }
}
