using System.Net;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>Every new web-app route requires an authenticated caller by default; <c>/source</c> is the one anonymous exception.</summary>
public sealed class WebAppAuthenticationTests
{
    /// <summary>The routes and methods the web-app endpoints add, with a session identifier that does not exist.</summary>
    public static TheoryData<string, string> Routes => new()
    {
        { "GET", "/sessions" },
        { "GET", "/board" },
        { "GET", "/workspace" },
        { "GET", "/events" },
        { "GET", "/sessions/x/stages" },
        { "GET", "/sessions/x/context" },
        { "GET", "/sessions/x/card-details" },
        { "GET", "/sessions/x/tool-approvals" },
        { "GET", "/sessions/x/config-options" },
        { "GET", "/sessions/x/commands" },
        { "GET", "/sessions/x/changes" },
        { "POST", "/sessions/x/close" },
        { "POST", "/sessions/x/reopen" },
        { "POST", "/sessions/x/resume" },
        { "POST", "/sessions/x/steer" },
        { "PUT", "/sessions/x/config-options/mode" },
        { "POST", "/sessions/x/changes/a.txt/accept" },
        { "POST", "/sessions/x/changes/a.txt/reject" },
    };

    /// <summary>With no bearer token each route answers 401.</summary>
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task WithoutAToken_IsUnauthorized(string method, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt();
        using var client = factory.CreateClient();

        using var response = await WebAppTestHost.SendAsync(client, new HttpMethod(method), path, string.Equals(method, "GET", StringComparison.Ordinal) ? null : "{}", ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>With a valid token the same routes reach their handler: an absent session is 404 (the list routes are 200).</summary>
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task WithAToken_ReachesTheHandler(string method, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt();
        using var client = WebAppTestHost.AsPrincipal(factory, "alice");
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
        if (!string.Equals(method, "GET", StringComparison.Ordinal))
        {
            request.Content = WebAppTestHost.Json("{\"text\":\"x\",\"mode\":\"queue\",\"value\":\"x\"}");
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(true);

        var expected = path is "/sessions" or "/board" or "/workspace" or "/events" ? HttpStatusCode.OK : HttpStatusCode.NotFound;
        response.StatusCode.ShouldBe(expected);
    }
}
