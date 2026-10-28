using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Autonogrammer.Api.Security;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.InMemory;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Session ownership on a Development host with the development principal off. A session belongs to the subject of the bearer
/// token that created it. Another subject's list, read, prompt, tool approval and event stream calls get the same 404 that an
/// absent session gets, and the owner's own calls succeed. A body owner id is refused with 400.
/// </summary>
public sealed class SessionOwnerScopeTests
{
    private const string Alice = "alice";
    private const string Bob = "bob";
    private const string ApprovalRequestId = "approval-scope-1";
    private const string AbsentId = "absent-session-id";
    private const string JsonType = "application/json";
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Verifies a session created by principal A is not listed for principal B and is listed for A.</summary>
    [Fact]
    public async Task List_ExcludesSessionsOwnedByAnotherPrincipal()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var alice = AsPrincipal(factory, Alice);
        using var bob = AsPrincipal(factory, Bob);
        var testToken = TestContext.Current.CancellationToken;

        var id = await SessionClient.CreateSessionAsync(alice, testToken).ConfigureAwait(true);

        (await ListIdsAsync(bob, testToken).ConfigureAwait(true)).ShouldBeEmpty();
        (await ListIdsAsync(alice, testToken).ConfigureAwait(true)).ShouldHaveSingleItem().ShouldBe(id);
    }

    /// <summary>Verifies principal B's read of principal A's session answers 404 with the same shape as an absent session.</summary>
    [Fact]
    public async Task Read_OtherPrincipalsSession_Returns404LikeAnAbsentSession()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var alice = AsPrincipal(factory, Alice);
        using var bob = AsPrincipal(factory, Bob);
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(alice, testToken).ConfigureAwait(true);

        using var owned = await bob.GetAsync(new Uri($"/sessions/{id}", UriKind.Relative), testToken).ConfigureAwait(true);
        using var absent = await bob.GetAsync(new Uri($"/sessions/{AbsentId}", UriKind.Relative), testToken).ConfigureAwait(true);

        var hidden = await NotFoundShapeAsync(owned, id, testToken).ConfigureAwait(true);
        hidden.ShouldBe(await NotFoundShapeAsync(absent, AbsentId, testToken).ConfigureAwait(true));
        hidden.Status.ShouldBe(HttpStatusCode.NotFound);
        hidden.Detail.ShouldNotContain(Alice);
    }

    /// <summary>Verifies principal B's prompt on principal A's session answers 404 and appends nothing, and A's own prompt is accepted.</summary>
    [Fact]
    public async Task Prompt_OtherPrincipalsSession_Returns404AndAppendsNothing()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var alice = AsPrincipal(factory, Alice);
        using var bob = AsPrincipal(factory, Bob);
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(alice, testToken).ConfigureAwait(true);

        using var refused = await SessionClient.PostPromptAsync(bob, id, "{\"prompt\":\"finish me\"}", testToken).ConfigureAwait(true);
        refused.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var untouched = await SessionClient.ReadSessionAsync(alice, id, testToken).ConfigureAwait(true);
        untouched.Status.ShouldBe(SessionStatus.Idle);
        untouched.Messages.ShouldBeEmpty();
        untouched.Events.ShouldBeEmpty();

        using var accepted = await SessionClient.PostPromptAsync(alice, id, "{\"prompt\":\"finish me\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await SessionClient.WaitForStatusAsync(alice, id, SessionStatus.Completed, SettleTimeout, testToken).ConfigureAwait(true);
    }

    /// <summary>Verifies principal B's event stream of principal A's session answers 404, and A's stream of the same session delivers events.</summary>
    [Fact]
    public async Task Events_OtherPrincipalsSession_Returns404AndOwnerStreams()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var alice = AsPrincipal(factory, Alice);
        using var bob = AsPrincipal(factory, Bob);
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(alice, testToken).ConfigureAwait(true);
        using (var accepted = await SessionClient.PostPromptAsync(alice, id, "{\"prompt\":\"finish me\"}", testToken).ConfigureAwait(true))
        {
            accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        await SessionClient.WaitForStatusAsync(alice, id, SessionStatus.Completed, SettleTimeout, testToken).ConfigureAwait(true);

        using var owned = await bob.GetAsync(new Uri($"/sessions/{id}/events", UriKind.Relative), testToken).ConfigureAwait(true);
        using var absent = await bob.GetAsync(new Uri($"/sessions/{AbsentId}/events", UriKind.Relative), testToken).ConfigureAwait(true);
        (await NotFoundShapeAsync(owned, id, testToken).ConfigureAwait(true))
            .ShouldBe(await NotFoundShapeAsync(absent, AbsentId, testToken).ConfigureAwait(true));

        var items = await SessionClient.ReadStreamAsync(alice, $"/sessions/{id}/events", 50, testToken).ConfigureAwait(true);
        items.ShouldNotBeEmpty();
    }

    /// <summary>Verifies principal B's decision on principal A's pending approval answers 404 and leaves it pending, and A's own decision is accepted.</summary>
    [Fact]
    public async Task ToolApproval_OtherPrincipalsSession_Returns404AndStaysPending()
    {
        using var factory = ApprovalFactory();
        using var alice = AsPrincipal(factory, Alice);
        using var bob = AsPrincipal(factory, Bob);
        var testToken = TestContext.Current.CancellationToken;
        var (id, requestId) = await StartAwaitingApprovalAsync(alice, testToken).ConfigureAwait(true);

        using (var refused = await PostDecisionAsync(bob, id, requestId, "{\"decision\":\"deny\"}", testToken).ConfigureAwait(true))
        {
            refused.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        var pending = await SessionClient.ReadSessionAsync(alice, id, testToken).ConfigureAwait(true);
        pending.Status.ShouldBe(SessionStatus.AwaitingToolApproval);

        using var denied = await PostDecisionAsync(alice, id, requestId, "{\"decision\":\"deny\"}", testToken).ConfigureAwait(true);
        denied.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await SessionClient.WaitForStatusAsync(alice, id, SessionStatus.Completed, SettleTimeout, testToken).ConfigureAwait(true);
    }

    /// <summary>Verifies a body owner id is refused with 400 for a bearer caller, and that no session is created for anyone.</summary>
    [Theory]
    [InlineData("{\"ownerId\":\"bob\"}")]
    [InlineData("{\"ownerId\":\"alice\"}")]
    [InlineData("{\"ownerId\":\"\"}")]
    public async Task Create_BodyOwnerId_Returns400AndCreatesNothing(string body)
    {
        using var factory = JwtTestHost.CreateFactory();
        using var alice = AsPrincipal(factory, Alice);
        using var bob = AsPrincipal(factory, Bob);
        var testToken = TestContext.Current.CancellationToken;

        using var content = new StringContent(body, Encoding.UTF8, JsonType);
        using var response = await alice.PostAsync(new Uri("/sessions", UriKind.Relative), content, testToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await ListIdsAsync(alice, testToken).ConfigureAwait(true)).ShouldBeEmpty();
        (await ListIdsAsync(bob, testToken).ConfigureAwait(true)).ShouldBeEmpty();
    }

    /// <summary>Verifies the development principal still creates, lists, reads and prompts its own sessions in Development.</summary>
    [Fact]
    public async Task DevelopmentPrincipal_OwnsAndSeesItsSessions()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        (await ListIdsAsync(client, testToken).ConfigureAwait(true)).ShouldHaveSingleItem().ShouldBe(id);
        var session = await SessionClient.ReadSessionAsync(client, id, testToken).ConfigureAwait(true);
        session.OwnerId.ShouldBe(SingleDevPrincipal.DevName);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"finish me\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, testToken).ConfigureAwait(true);
    }

    private static ApiFactory ApprovalFactory() => new(JwtTestHost.DevelopmentConfig(), services =>
    {
        JwtTestHost.InjectStaticMetadata(services);
        services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(ApprovalScript()));
    });

    /// <summary>A permission request the run routes into approval, then the completion that ends the turn.</summary>
    private static InMemoryScript ApprovalScript() => new()
    {
        Steps =
        [
            new EmitStep(new PermissionRequestEvent("call-1", "Bash", "{\"command\":\"ls\"}", ApprovalRequestId)),
            new EmitStep(new CompletedEvent("end_turn")),
        ],
    };

    /// <summary>Returns a client that authenticates as <paramref name="subject"/> against the factory's host.</summary>
    private static HttpClient AsPrincipal(ApiFactory factory, string subject)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken(subject));
        return client;
    }

    private static async Task<List<string>> ListIdsAsync(HttpClient client, CancellationToken testToken)
    {
        using var response = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(false);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(testToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.EnumerateArray()
            .Select(static element => element.GetProperty("id").GetString().ShouldNotBeNull())
            .ToList();
    }

    /// <summary>
    /// Returns the parts of a 404 that a caller can observe, with the requested id replaced by a placeholder. Two answers with equal
    /// shapes differ only by the id the caller sent, so the body reveals nothing about another owner's session.
    /// </summary>
    private static async Task<NotFoundShape> NotFoundShapeAsync(HttpResponseMessage response, string requestedId, CancellationToken testToken)
    {
        var body = await response.Content.ReadAsStringAsync(testToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return new NotFoundShape(
            response.StatusCode,
            response.Content.Headers.ContentType?.MediaType,
            root.GetProperty("title").GetString().ShouldNotBeNull(),
            root.GetProperty("detail").GetString().ShouldNotBeNull().Replace(requestedId, "{id}", StringComparison.Ordinal));
    }

    private static async Task<(string Id, string RequestId)> StartAwaitingApprovalAsync(HttpClient client, CancellationToken testToken)
    {
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(false);
        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"run\"}", testToken).ConfigureAwait(false);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var requestId = await WaitForPendingApprovalAsync(client, id, testToken).ConfigureAwait(false);
        return (id, requestId);
    }

    /// <summary>
    /// Waits until the pending request is in the session log and returns its request id. The status is saved before the request event
    /// is appended, so the decision waits for the event as well. The id is read from the event, not assumed from the script.
    /// </summary>
    private static async Task<string> WaitForPendingApprovalAsync(HttpClient client, string sessionId, CancellationToken testToken)
    {
        using var timeout = new CancellationTokenSource(SettleTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, testToken);
        while (true)
        {
            var session = await SessionClient.ReadSessionAsync(client, sessionId, linked.Token).ConfigureAwait(false);
            var requests = session.Events
                .Select(static evt => evt.Payload)
                .OfType<UnknownPayload>()
                .Where(static payload => string.Equals(payload.OriginalKind, "tool_approval_requested", StringComparison.Ordinal))
                .ToArray();
            if (requests.Length > 0)
            {
                return requests[0].Raw.GetProperty("requestId").GetString().ShouldNotBeNull();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), linked.Token).ConfigureAwait(false);
        }
    }

    private static async Task<HttpResponseMessage> PostDecisionAsync(HttpClient client, string sessionId, string requestId, string body, CancellationToken testToken)
    {
        using var content = new StringContent(body, Encoding.UTF8, JsonType);
        return await client.PostAsync(new Uri($"/sessions/{sessionId}/tool-approvals/{requestId}", UriKind.Relative), content, testToken).ConfigureAwait(false);
    }

    /// <summary>The observable parts of a not-found answer, with the requested id replaced by <c>{id}</c>.</summary>
    private sealed record NotFoundShape(HttpStatusCode Status, string? MediaType, string Title, string Detail);
}
