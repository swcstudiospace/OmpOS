using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Session CRUD plus the full prompt validation matrix: 415, 413, 400, 404, 409, 429
/// with Retry-After, CORS lockout, and correlation round-trips.
/// </summary>
public sealed class SessionEndpointTests
{
    /// <summary>Verifies session creation returns 201 with the session and a location.</summary>
    [Fact]
    public async Task Create_Returns201WithSession()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(new Uri("/sessions", UriKind.Relative), content, TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.GetValues("x-correlation-id").ShouldNotBeEmpty();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("ownerId").GetString().ShouldBe("local-dev");
    }

    /// <summary>Verifies the session owner is the subject of the caller's bearer token, not a requested owner.</summary>
    [Fact]
    public async Task Create_WithPrincipalSubject_OwnerIsTheSubject()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));

        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);
        var session = await SessionClient.ReadSessionAsync(client, id, testToken).ConfigureAwait(true);

        session.OwnerId.ShouldBe("alice");
    }

    /// <summary>Verifies session reads and the 404 shape.</summary>
    [Fact]
    public async Task Get_RoundTripsAndUnknownIs404Problem()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var found = await client.GetAsync(new Uri($"/sessions/{id}", UriKind.Relative), testToken).ConfigureAwait(true);
        found.StatusCode.ShouldBe(HttpStatusCode.OK);
        found.Headers.GetValues("x-correlation-id").ShouldNotBeEmpty();

        using var missing = await client.GetAsync(new Uri("/sessions/does-not-exist", UriKind.Relative), testToken).ConfigureAwait(true);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        missing.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        missing.Headers.GetValues("x-correlation-id").ShouldNotBeEmpty();
    }

    /// <summary>Verifies listing returns every created session.</summary>
    [Fact]
    public async Task List_ReturnsCreatedSessions()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var first = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);
        var second = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var response = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(testToken).ConfigureAwait(true);
        using var document = JsonDocument.Parse(body);
        var ids = document.RootElement.EnumerateArray().Select(static element => element.GetProperty("id").GetString()).ToList();
        ids.Contains(first, StringComparer.Ordinal).ShouldBeTrue();
        ids.Contains(second, StringComparer.Ordinal).ShouldBeTrue();
    }

    /// <summary>Verifies a non-JSON content type is rejected with 415.</summary>
    [Fact]
    public async Task Prompt_WrongContentType_Returns415()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var response = await SessionClient.PostPromptAsync(client, id, "hello", testToken, "text/plain").ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    /// <summary>Verifies an oversized body is rejected with 413.</summary>
    [Fact]
    public async Task Prompt_OversizedBody_Returns413()
    {
        using var factory = new ApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:MaxRequestBodyBytes"] = "512" });
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var response = await SessionClient.PostPromptAsync(client, id, $"{{\"prompt\":\"{new string('x', 600)}\"}}", testToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    /// <summary>Verifies empty, blank, missing, and malformed prompts are rejected with 400.</summary>
    [Theory]
    [InlineData("{\"prompt\":\"\"}")]
    [InlineData("{\"prompt\":\"   \"}")]
    [InlineData("{}")]
    [InlineData("not json")]
    public async Task Prompt_InvalidShapes_Return400(string body)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var response = await SessionClient.PostPromptAsync(client, id, body, testToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>Verifies an over-length prompt is rejected with 400.</summary>
    [Fact]
    public async Task Prompt_Oversize_Returns400()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var response = await SessionClient.PostPromptAsync(client, id, $"{{\"prompt\":\"{new string('x', 64001)}\"}}", testToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>Verifies prompts on unknown sessions return 404.</summary>
    [Fact]
    public async Task Prompt_UnknownSession_Returns404()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await SessionClient.PostPromptAsync(client, "does-not-exist", "{\"prompt\":\"hi\"}", TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>Verifies a second prompt while one is in flight is rejected with 409 ProblemDetails.</summary>
    /// <remarks>A slash command is not steerable, so it still takes the prompt-admission path and 409s.</remarks>
    [Fact]
    public async Task Prompt_WhileInFlight_Returns409()
    {
        var gate = new GatedHarnessFactory();
        using var factory = new ApiFactory(configureServices: services => services.AddSingleton<IHarnessSessionFactory>(gate));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var first = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"one\"}", testToken).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        using var second = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"/resume\"}", testToken).ConfigureAwait(true);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        second.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        gate.Release();
        var settled = await SessionClient.WaitForStatusAsync(client, id, Contracts.Sessions.SessionStatus.Completed, TimeSpan.FromSeconds(15), testToken).ConfigureAwait(true);
        settled.Messages.Count.ShouldBe(1, "the rejected prompt is never queued");
    }

    /// <summary>Verifies the token bucket rejects over-budget prompts with 429 and Retry-After.</summary>
    [Fact]
    public async Task Prompt_OverBudget_Returns429WithRetryAfter()
    {
        using var factory = new ApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:RateLimit:PromptsPerMinute"] = "2" });
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            using var response = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hi\"}", testToken).ConfigureAwait(false);
            return (Status: response.StatusCode, RetryAfter: response.Headers.RetryAfter);
        })).ConfigureAwait(true);

        attempts.Select(static attempt => attempt.Status).ShouldContain(HttpStatusCode.TooManyRequests);
        foreach (var attempt in attempts.Where(static attempt => attempt.Status == HttpStatusCode.TooManyRequests))
        {
            attempt.RetryAfter.ShouldNotBeNull();
        }
    }

    /// <summary>Verifies disallowed origins get no CORS headers while allowed origins do.</summary>
    [Fact]
    public async Task Cors_DisallowedOriginGetsNoHeaders()
    {
        using var factory = new ApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:Cors:Origins:0"] = "https://allowed.example" });
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var denied = new HttpRequestMessage(HttpMethod.Get, new Uri("/sessions", UriKind.Relative));
        denied.Headers.Add("Origin", "https://evil.example");
        using var deniedResponse = await client.SendAsync(denied, testToken).ConfigureAwait(true);
        deniedResponse.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();

        using var allowed = new HttpRequestMessage(HttpMethod.Get, new Uri("/sessions", UriKind.Relative));
        allowed.Headers.Add("Origin", "https://allowed.example");
        using var allowedResponse = await client.SendAsync(allowed, testToken).ConfigureAwait(true);
        allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Contains("https://allowed.example", StringComparer.Ordinal).ShouldBeTrue();
    }

    /// <summary>Verifies the correlation id round-trips when supplied and is minted when absent.</summary>
    [Fact]
    public async Task CorrelationId_RoundTrips()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var correlation = Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("x-correlation-id", correlation);

        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(new Uri("/sessions", UriKind.Relative), content, testToken).ConfigureAwait(true);
        response.Headers.GetValues("x-correlation-id").Contains(correlation, StringComparer.Ordinal).ShouldBeTrue();

        using var unshapedFactory = new ApiFactory();
        using var plain = unshapedFactory.CreateClient();
        plain.DefaultRequestHeaders.Add("x-correlation-id", "not a valid id");
        using var minted = await plain.GetAsync(new Uri("/health/live", UriKind.Relative), testToken).ConfigureAwait(true);
        var echoed = string.Join(",", minted.Headers.GetValues("x-correlation-id"));
        Guid.TryParse(echoed, out _).ShouldBeTrue("an invalid inbound id must be replaced with a minted one");
        string.Equals(echoed, "not a valid id", StringComparison.Ordinal).ShouldBeFalse("an invalid inbound id must be replaced with a minted one");

        using var bareFactory = new ApiFactory();
        using var bare = bareFactory.CreateClient();
        using var anonymous = await bare.GetAsync(new Uri("/health/live", UriKind.Relative), testToken).ConfigureAwait(true);
        anonymous.Headers.GetValues("x-correlation-id").ShouldNotBeEmpty();
    }
}
