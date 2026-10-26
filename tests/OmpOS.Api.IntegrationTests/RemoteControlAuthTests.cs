using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OmpOS.Contracts.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// ADE-08 remote-control auth: the session operations a second device calls to create a session, send a prompt, decide a tool
/// approval and read the event stream answer 401 without a bearer token. The production-shaped host refuses them, and so does a
/// Development host with the development principal disabled, where a refused call leaves the session unchanged. The session
/// list is covered by <see cref="ApiAuthBoundaryTests"/> and <see cref="ApiAuthStartupTests"/>, so it is not repeated here.
/// </summary>
public sealed class RemoteControlAuthTests
{
    private const string UnknownSessionId = "remote-control-session";
    private const string RequestId = "remote-control-request";
    private const string PromptBody = "{\"prompt\":\"finish me\"}";
    private const string ApprovalBody = "{\"decision\":\"allow\"}";

    /// <summary>Verifies each session operation a second device calls answers 401 on a production-shaped host without a bearer token.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The relative request path.</param>
    /// <param name="body">The JSON body, or empty for a bodyless request.</param>
    [Theory]
    [InlineData("POST", "/sessions", "{}")]
    [InlineData("POST", "/sessions/" + UnknownSessionId + "/prompts", PromptBody)]
    [InlineData("POST", "/sessions/" + UnknownSessionId + "/tool-approvals/" + RequestId, ApprovalBody)]
    [InlineData("GET", "/sessions/" + UnknownSessionId + "/events", "")]
    public async Task ProductionHost_AnonymousSessionOperation_Returns401(string method, string path, string body)
    {
        using var factory = new OmpApiFactory(JwtTestHost.ProductionConfig(), environment: "Production");
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var response = await SendAsync(client, method, path, body, testToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies that on a Development host with the development principal disabled, anonymous create, prompt, approval and event
    /// requests answer 401, and that the refused requests change nothing: the owner's session stays idle with no messages and no
    /// second session is created.
    /// </summary>
    [Fact]
    public async Task DevelopmentHost_DevelopmentPrincipalDisabled_RefusesAnonymousSessionOperations()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var anonymous = factory.CreateClient();
        using var authenticated = factory.CreateClient();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        var testToken = TestContext.Current.CancellationToken;
        var sessionId = await SessionClient.CreateSessionAsync(authenticated, testToken).ConfigureAwait(true);

        using var created = await SendAsync(anonymous, "POST", "/sessions", "{}", testToken).ConfigureAwait(true);
        using var prompted = await SendAsync(anonymous, "POST", $"/sessions/{sessionId}/prompts", PromptBody, testToken).ConfigureAwait(true);
        using var approved = await SendAsync(anonymous, "POST", $"/sessions/{sessionId}/tool-approvals/{RequestId}", ApprovalBody, testToken).ConfigureAwait(true);
        using var events = await SendAsync(anonymous, "GET", $"/sessions/{sessionId}/events", string.Empty, testToken).ConfigureAwait(true);

        created.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        prompted.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        approved.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        events.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var session = await SessionClient.ReadSessionAsync(authenticated, sessionId, testToken).ConfigureAwait(true);
        session.Status.ShouldBe(SessionStatus.Idle);
        session.Messages.ShouldBeEmpty();
        session.OwnerId.ShouldBe("alice");

        using var list = await authenticated.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(true);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await list.Content.ReadAsStringAsync(testToken).ConfigureAwait(true);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetArrayLength().ShouldBe(1);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path, string body, CancellationToken testToken)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
        if (body.Length > 0)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request, testToken).ConfigureAwait(false);
    }
}
