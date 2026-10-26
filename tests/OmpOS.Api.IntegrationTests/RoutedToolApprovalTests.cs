using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;
using OmpOS.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Tool approvals whose request was registered the way a routed run registers it: through <see cref="SessionService"/>, with
/// no harness run in this process. The decision endpoint resolves the request through the session store, so its outcome does not
/// depend on which process registered the request. Each outcome maps to its documented status, and the stored decision is the
/// one the endpoint wrote.
/// </summary>
public sealed class RoutedToolApprovalTests
{
    private const string RequestId = "routed-approval-1";
    private const string ToolName = "Bash";
    private const string JsonType = "application/json";
    private const string AllowBody = "{\"decision\":\"allow\"}";
    private const string DenyBody = "{\"decision\":\"deny\"}";

    /// <summary>An allow for a tool the ceiling does not grant is 403, and the request stays pending in the store.</summary>
    [Fact]
    public async Task AllowAboveCeiling_IsForbidden_AndRequestStaysPending()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var id = await PendingRequestAsync(factory, client, grantableTools: null, ct).ConfigureAwait(true);

        using (var forbidden = await PostDecisionAsync(client, id, RequestId, AllowBody, ct).ConfigureAwait(true))
        {
            forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        var session = await SessionClient.ReadSessionAsync(client, id, ct).ConfigureAwait(true);
        session.Status.ShouldBe(SessionStatus.AwaitingToolApproval);
        var record = await factory.Services.GetRequiredService<ISessionRepository>().GetToolApprovalAsync(id, RequestId, ct).ConfigureAwait(true);
        record.ShouldNotBeNull().Decision.ShouldBeNull();
    }

    /// <summary>An allow within the ceiling is 204, resumes the session, and stores the allow.</summary>
    [Fact]
    public async Task GrantedAllow_IsNoContent_ResumesSession_AndIsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var id = await PendingRequestAsync(factory, client, new HashSet<string>(StringComparer.Ordinal) { ToolName }, ct).ConfigureAwait(true);

        using var allowed = await PostDecisionAsync(client, id, RequestId, AllowBody, ct).ConfigureAwait(true);

        allowed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var session = await SessionClient.ReadSessionAsync(client, id, ct).ConfigureAwait(true);
        session.Status.ShouldBe(SessionStatus.Running);
        var record = await factory.Services.GetRequiredService<ISessionRepository>().GetToolApprovalAsync(id, RequestId, ct).ConfigureAwait(true);
        record.ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Allow);
    }

    /// <summary>A deny is 204 and is stored once: a second decision for the same request is 409 and does not change the stored decision.</summary>
    [Fact]
    public async Task Deny_IsNoContent_AndSecondDecisionIsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var id = await PendingRequestAsync(factory, client, grantableTools: null, ct).ConfigureAwait(true);

        using (var denied = await PostDecisionAsync(client, id, RequestId, DenyBody, ct).ConfigureAwait(true))
        {
            denied.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using (var repeat = await PostDecisionAsync(client, id, RequestId, AllowBody, ct).ConfigureAwait(true))
        {
            repeat.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        var record = await factory.Services.GetRequiredService<ISessionRepository>().GetToolApprovalAsync(id, RequestId, ct).ConfigureAwait(true);
        record.ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Deny);
    }

    /// <summary>A decision for a session that does not exist is 404.</summary>
    [Fact]
    public async Task DecisionForMissingSession_IsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostDecisionAsync(client, "missing-session", RequestId, DenyBody, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>An allow within the ceiling resumes the run, and the prompt then settles as completed.</summary>
    [Fact]
    public async Task GrantedAllow_ResumesRun_AndPromptSettlesCompleted()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var id = await PendingRequestAsync(factory, client, new HashSet<string>(StringComparer.Ordinal) { ToolName }, ct).ConfigureAwait(true);

        using (var allowed = await PostDecisionAsync(client, id, RequestId, AllowBody, ct).ConfigureAwait(true))
        {
            allowed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await factory.Services.GetRequiredService<SessionService>().RecordCompletionAsync(id, SessionStatus.Completed, ct).ConfigureAwait(true);
        var settled = await SessionClient.ReadSessionAsync(client, id, ct).ConfigureAwait(true);
        settled.Status.ShouldBe(SessionStatus.Completed);
        var stored = await factory.Services.GetRequiredService<ISessionRepository>().GetAsync(id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        EventKinds(stored).ShouldContain("tool_approval_approved", StringComparer.Ordinal);
    }

    /// <summary>An approval is decided once: a repeated approval is a 409, and the stored decision stays an allow.</summary>
    [Fact]
    public async Task GrantedAllow_RepeatedApprovalIsConflict_AndStoredDecisionStaysAllow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var id = await PendingRequestAsync(factory, client, new HashSet<string>(StringComparer.Ordinal) { ToolName }, ct).ConfigureAwait(true);

        using (var allowed = await PostDecisionAsync(client, id, RequestId, AllowBody, ct).ConfigureAwait(true))
        {
            allowed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using (var repeat = await PostDecisionAsync(client, id, RequestId, AllowBody, ct).ConfigureAwait(true))
        {
            repeat.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        var record = await factory.Services.GetRequiredService<ISessionRepository>().GetToolApprovalAsync(id, RequestId, ct).ConfigureAwait(true);
        record.ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Allow);
    }

    /// <summary>With no grant, a request that no decision reaches is denied when its wait lapses; a later approval is a 409 that grants nothing.</summary>
    [Fact]
    public async Task NoGrant_LapsedWaitDeniesRequest_AndLaterApprovalIsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var id = await PendingRequestAsync(factory, client, grantableTools: null, ct).ConfigureAwait(true);

        var outcome = await factory.Services.GetRequiredService<SessionService>().WaitForDecisionAsync(id, RequestId, TimeSpan.FromMilliseconds(50), ct).ConfigureAwait(true);
        outcome.ShouldBe(PermissionDecision.Deny);

        using (var late = await PostDecisionAsync(client, id, RequestId, AllowBody, ct).ConfigureAwait(true))
        {
            late.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        var record = await factory.Services.GetRequiredService<ISessionRepository>().GetToolApprovalAsync(id, RequestId, ct).ConfigureAwait(true);
        record.ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Deny);
        var session = await SessionClient.ReadSessionAsync(client, id, ct).ConfigureAwait(true);
        session.Status.ShouldBe(SessionStatus.Running);
    }

    private static string?[] EventKinds(Session session) =>
        session.Events
            .Select(static evt => evt.Payload)
            .OfType<UnknownPayload>()
            .Select(static payload => payload.OriginalKind)
            .ToArray();

    /// <summary>
    /// Creates a session whose run is awaiting approval for <see cref="RequestId"/>. The request is registered through
    /// <see cref="SessionService"/> directly, which is how a routed run registers it; no harness run is started here.
    /// </summary>
    /// <param name="factory">The API host.</param>
    /// <param name="client">The HTTP client for the host.</param>
    /// <param name="grantableTools">The request's ceiling; null is the default (no tool may be allowed).</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>The session identifier.</returns>
    private static async Task<string> PendingRequestAsync(OmpApiFactory factory, HttpClient client, IReadOnlySet<string>? grantableTools, CancellationToken ct)
    {
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(false);
        var repository = factory.Services.GetRequiredService<ISessionRepository>();
        var session = await repository.GetAsync(id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Session '{id}' was not created.");

        // The state a routed prompt leaves behind. No in-process run is started, so nothing else moves the session.
        await repository.SaveAsync(session with { Status = SessionStatus.Running, UpdatedAt = TimeProvider.System.GetUtcNow() }, ct).ConfigureAwait(false);

        var sessions = factory.Services.GetRequiredService<SessionService>();
        await sessions.RequestToolApprovalAsync(id, RequestId, ToolName, ct, grantableTools).ConfigureAwait(false);
        return id;
    }

    private static async Task<HttpResponseMessage> PostDecisionAsync(HttpClient client, string sessionId, string requestId, string body, CancellationToken ct)
    {
        using var content = new StringContent(body, Encoding.UTF8, JsonType);
        return await client.PostAsync(new Uri($"/sessions/{sessionId}/tool-approvals/{requestId}", UriKind.Relative), content, ct).ConfigureAwait(false);
    }
}
