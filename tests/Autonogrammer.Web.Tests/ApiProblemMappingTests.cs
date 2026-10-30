using System.Net;
using System.Text;
using Autonogrammer.Web.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>A problem response becomes an <see cref="ApiProblemException"/> with the right kind, the problem code, a bounded detail, and no prompt or body in the message.</summary>
public sealed class ApiProblemMappingTests
{
    private const string SessionJson = """{"id":"s-1","ownerId":"o","createdAt":"2026-10-27T00:00:00+00:00","updatedAt":"2026-10-27T00:00:00+00:00","status":"Idle","messages":[],"events":[]}""";

    /// <summary>Each documented problem status maps to its kind.</summary>
    /// <param name="status">The HTTP status.</param>
    /// <param name="kindName">The name of the expected <see cref="ApiProblemKind"/> (the enum is internal, a public test method cannot take it).</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData(400, nameof(ApiProblemKind.BadRequest))]
    [InlineData(401, nameof(ApiProblemKind.Unauthorized))]
    [InlineData(403, nameof(ApiProblemKind.Forbidden))]
    [InlineData(404, nameof(ApiProblemKind.NotFound))]
    [InlineData(409, nameof(ApiProblemKind.Conflict))]
    [InlineData(413, nameof(ApiProblemKind.PayloadTooLarge))]
    [InlineData(415, nameof(ApiProblemKind.UnsupportedMediaType))]
    [InlineData(422, nameof(ApiProblemKind.Unprocessable))]
    [InlineData(429, nameof(ApiProblemKind.RateLimited))]
    [InlineData(502, nameof(ApiProblemKind.Server))]
    [InlineData(418, nameof(ApiProblemKind.Other))]
    public async Task EveryStatusMapsToItsKind(int status, string kindName)
    {
        var kind = Enum.Parse<ApiProblemKind>(kindName);
        using var handler = new RoutedHandler();
        handler.OnProblem(HttpMethod.Get, "/sessions/s-1", (HttpStatusCode)status, "some_code", "Title.", "Detail.");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };
        var client = new OmpApiClient(http);

        var problem = await Should.ThrowAsync<ApiProblemException>(() => client.GetSessionAsync("s-1", TestContext.Current.CancellationToken)).ConfigureAwait(true);

        problem.Kind.ShouldBe(kind);
        problem.StatusCode.ShouldBe((HttpStatusCode)status);
        problem.Code.ShouldBe("some_code");
        problem.Title.ShouldBe("Title.");
        problem.Detail.ShouldBe("Detail.");
    }

    /// <summary>A 429 keeps the Retry-After delay.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task RateLimitKeepsRetryAfter()
    {
        using var handler = new RoutedHandler();
        handler.On(HttpMethod.Post, "/sessions/s-1/prompts", _ =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)429) { Content = new StringContent("{}", Encoding.UTF8, "application/problem+json") };
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
            return response;
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

        var problem = await Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(http).SubmitPromptAsync("s-1", "hello", TestContext.Current.CancellationToken)).ConfigureAwait(true);

        problem.Kind.ShouldBe(ApiProblemKind.RateLimited);
        problem.RetryAfter.ShouldBe(TimeSpan.FromSeconds(7));
    }

    /// <summary>A body that is not a problem document still maps by status, with a default title.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task NonJsonBodyStillMapsByStatus()
    {
        using var handler = new RoutedHandler();
        handler.On(HttpMethod.Get, "/board", _ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>bad gateway</html>", Encoding.UTF8, "text/html") });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

        var problem = await Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(http).GetBoardAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);

        problem.Kind.ShouldBe(ApiProblemKind.Server);
        problem.Code.ShouldBeNull();
        problem.Message.ShouldNotContain("html");
    }

    /// <summary>A long detail is cut, and neither the prompt that was sent nor the response body ends up in the message.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task MessageNeverCarriesThePromptOrTheBody()
    {
        const string prompt = "SECRET-PROMPT-TEXT";
        using var handler = new RoutedHandler();
        handler.OnProblem(HttpMethod.Post, "/sessions/s-1/prompts", HttpStatusCode.Conflict, "session_closed", "Session is closed.", new string('d', 900));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

        var problem = await Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(http).SubmitPromptAsync("s-1", prompt, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        handler.Bodies.Single().ShouldContain(prompt, Case.Sensitive);
        problem.Detail!.Length.ShouldBe(ApiProblemException.MaxDetailChars);
        problem.Message.ShouldNotContain(prompt);
        problem.ToString().ShouldNotContain(prompt);
        problem.Message.ShouldNotContain(new string('d', 20));
    }

    /// <summary>A success with a body that is not the documented shape is a client exception, not a crash.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task MalformedSuccessBodyIsAClientException()
    {
        using var handler = new RoutedHandler();
        handler.OnJson(HttpMethod.Get, "/workspace", "[1,2,3]");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

        var failure = await Should.ThrowAsync<ApiClientException>(() => new OmpApiClient(http).GetWorkspaceAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);

        failure.ShouldNotBeOfType<ApiProblemException>();
    }

    /// <summary>The token provider seam: no token sends no Authorization header; a token sends Bearer and never appears in a failure message.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task BearerTokenIsSentWhenProvidedAndNeverLeaksIntoErrors()
    {
        const string token = "TOKEN-VALUE-123";
        using var routes = new RoutedHandler();
        routes.OnJson(HttpMethod.Get, "/sessions/s-1", SessionJson);
        routes.OnProblem(HttpMethod.Get, "/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
        using var anonymousHandler = new BearerTokenHandler(new NoApiTokenProvider(), routes);
        using var signedInHandler = new BearerTokenHandler(new FixedToken(token), routes);
        using var anonymous = new HttpClient(anonymousHandler, disposeHandler: false) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };
        using var signedIn = new HttpClient(signedInHandler, disposeHandler: false) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

        _ = await new OmpApiClient(anonymous).GetSessionAsync("s-1", TestContext.Current.CancellationToken).ConfigureAwait(true);
        var failure = await Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(signedIn).GetBoardAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);

        routes.Headers[0].ContainsKey("Authorization").ShouldBeFalse();
        routes.Headers[1]["Authorization"].ShouldBe("Bearer " + token);
        failure.Kind.ShouldBe(ApiProblemKind.Unauthorized);
        failure.ToString().ShouldNotContain(token);
    }

    /// <summary>A cancelled token cancels the call.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task CancellationIsHonoured()
    {
        using var handler = new RoutedHandler();
        handler.OnJson(HttpMethod.Get, "/board", "{}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(() => new OmpApiClient(http).GetBoardAsync(cancelled.Token)).ConfigureAwait(true);
    }

    private sealed class FixedToken(string token) : IApiTokenProvider
    {
        public ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(token);
    }
}
