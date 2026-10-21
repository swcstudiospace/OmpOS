using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.InMemory;
using OmpOS.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Tool approval over HTTP on the direct path: a scripted permission request holds the run until a decision arrives, and
/// the decision endpoint maps each outcome to its status. Also covers the in-memory permission pause.
/// </summary>
public sealed class ToolApprovalEndpointTests
{
    private const string RequestId = "approval-1";
    private const string JsonType = "application/json";
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>A malformed decision body is a 400, and the request stays pending.</summary>
    [Fact]
    public async Task MalformedDecisionBodyIsRejectedWith400AndRequestStaysPending()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = ApprovalFactory();
        using var client = factory.CreateClient();
        var id = await StartAwaitingApprovalAsync(client, ct);
        var requestId = await PendingRequestIdAsync(client, id, ct);

        string[] bodies = ["not json", string.Empty, "{}", "[]", "{\"decision\":\"maybe\"}", "{\"decision\":true}", "{\"decision\":\"allow\",\"extra\":1}", "{\"decision\":\"allow\",\"decision\":\"allow\"}"];
        foreach (var body in bodies)
        {
            using var response = await PostDecisionAsync(client, id, requestId, body, JsonType, ct);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"body: {body}");
        }

        var session = await SessionClient.ReadSessionAsync(client, id, ct);
        session.Status.ShouldBe(SessionStatus.AwaitingToolApproval);
    }

    /// <summary>A body that is not JSON is refused by content type before it is read.</summary>
    [Fact]
    public async Task NonJsonContentTypeIsRejectedWith415()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = ApprovalFactory();
        using var client = factory.CreateClient();
        var id = await StartAwaitingApprovalAsync(client, ct);
        var requestId = await PendingRequestIdAsync(client, id, ct);

        using var response = await PostDecisionAsync(client, id, requestId, "{\"decision\":\"deny\"}", "text/plain", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    /// <summary>An allow exceeds the default ceiling, which grants nothing: 403, and the request stays pending for a denial.</summary>
    [Fact]
    public async Task AllowAboveDefaultCeilingIsForbiddenAndRequestStaysPending()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = ApprovalFactory();
        using var client = factory.CreateClient();
        var id = await StartAwaitingApprovalAsync(client, ct);
        var requestId = await PendingRequestIdAsync(client, id, ct);

        using (var forbidden = await PostDecisionAsync(client, id, requestId, "{\"decision\":\"allow\"}", JsonType, ct))
        {
            forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        var stillAwaiting = await SessionClient.ReadSessionAsync(client, id, ct);
        stillAwaiting.Status.ShouldBe(SessionStatus.AwaitingToolApproval);

        using var denied = await PostDecisionAsync(client, id, requestId, "{\"decision\":\"deny\"}", JsonType, ct);
        denied.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>A denial resolves the request once, the prompt completes, and a second decision is a 409.</summary>
    [Fact]
    public async Task DenialResolvesRequestOnceAndPromptCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = ApprovalFactory();
        using var client = factory.CreateClient();
        var id = await StartAwaitingApprovalAsync(client, ct);
        var requestId = await PendingRequestIdAsync(client, id, ct);

        using (var denied = await PostDecisionAsync(client, id, requestId, "{\"decision\":\"deny\"}", JsonType, ct))
        {
            denied.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var completed = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, ct);
        completed.Status.ShouldBe(SessionStatus.Completed);
        var stored = await factory.Services.GetRequiredService<ISessionRepository>().GetAsync(id, ct);
        stored.ShouldNotBeNull();
        EventKinds(stored).ShouldContain("tool_approval_requested", StringComparer.Ordinal);
        EventKinds(stored).ShouldContain("tool_approval_denied", StringComparer.Ordinal);

        using var repeat = await PostDecisionAsync(client, id, requestId, "{\"decision\":\"deny\"}", JsonType, ct);
        repeat.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    /// <summary>An unknown request id while the session awaits approval is a 409, and the request stays pending.</summary>
    [Fact]
    public async Task UnknownRequestIdIsConflictAndRequestStaysPending()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = ApprovalFactory();
        using var client = factory.CreateClient();
        var id = await StartAwaitingApprovalAsync(client, ct);

        using (var unknown = await PostDecisionAsync(client, id, "absent", "{\"decision\":\"deny\"}", JsonType, ct))
        {
            unknown.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        var session = await SessionClient.ReadSessionAsync(client, id, ct);
        session.Status.ShouldBe(SessionStatus.AwaitingToolApproval);
    }

    /// <summary>A decision for a session that does not exist is a 404.</summary>
    [Fact]
    public async Task DecisionForMissingSessionIsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = ApprovalFactory();
        using var client = factory.CreateClient();

        using var response = await PostDecisionAsync(client, "missing-session", RequestId, "{\"decision\":\"deny\"}", JsonType, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>The in-memory pause emits its request and holds the stream until the resolver answers.</summary>
    [Fact]
    public async Task PermissionPauseHoldsStreamUntilResolverAnswers()
    {
        var ct = TestContext.Current.CancellationToken;
        var resolver = new GateResolver();
        await using var session = PauseSession(resolver, TimeSpan.FromSeconds(30));
        await session.CreateAsync(ct);
        await using var events = session.SubmitPromptAsync("run", new PromptOptions { Steer = false }, ct).GetAsyncEnumerator(ct);

        (await events.MoveNextAsync()).ShouldBeTrue();
        events.Current.ShouldBeOfType<PermissionRequestEvent>().RequestId.ShouldBe(RequestId);
        var next = events.MoveNextAsync().AsTask();

        await resolver.WaitForEntryAsync(SettleTimeout, ct);
        next.IsCompleted.ShouldBeFalse("the stream must hold while the decision is pending");
        resolver.Answer(PermissionDecision.Allow);

        (await next.ConfigureAwait(true)).ShouldBeTrue();
        events.Current.ShouldBeOfType<CompletedEvent>();
        (await events.MoveNextAsync().ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>A pause whose resolver never answers ends at its timeout without a grant, and the stream completes.</summary>
    [Fact]
    public async Task PermissionPauseEndsAtTimeoutWithoutGrant()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = PauseSession(new SilentResolver(), TimeSpan.FromMilliseconds(50));
        await session.CreateAsync(ct);
        await using var events = session.SubmitPromptAsync("run", new PromptOptions { Steer = false }, ct).GetAsyncEnumerator(ct);

        var seen = new List<HarnessEvent>();
        while (await events.MoveNextAsync())
        {
            seen.Add(events.Current);
        }

        seen.Count.ShouldBe(2);
        seen[0].ShouldBeOfType<PermissionRequestEvent>();
        seen[1].ShouldBeOfType<CompletedEvent>();
    }

    private static OmpApiFactory ApprovalFactory() =>
        new(configureServices: services => services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(ApprovalScript())));

    /// <summary>A permission request the run routes into approval, then the completion that ends the turn.</summary>
    private static InMemoryScript ApprovalScript() => new()
    {
        Steps =
        [
            new EmitStep(new PermissionRequestEvent("call-1", "Bash", "{\"command\":\"ls\"}", RequestId)),
            new EmitStep(new CompletedEvent("end_turn")),
        ],
    };

    private static IOmpSession PauseSession(IPermissionResolver resolver, TimeSpan timeout) =>
        new InMemoryOmpAdapter(new InMemoryScript
        {
            Steps =
            [
                new PermissionPauseStep(new PermissionRequestEvent("call-1", "Bash", "{}", RequestId)),
                new EmitStep(new CompletedEvent("end_turn")),
            ],
            PermissionResolver = resolver,
            PermissionDecisionTimeout = timeout,
        }).CreateSession();

    private static async Task<string> StartAwaitingApprovalAsync(HttpClient client, CancellationToken ct)
    {
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(false);
        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"run\"}", ct).ConfigureAwait(false);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.AwaitingToolApproval, SettleTimeout, ct).ConfigureAwait(false);
        return id;
    }

    /// <summary>
    /// Reads the identifier of the pending tool approval from the session event log, as a UI does. The session status is
    /// saved before the request event is appended, so this waits until that single event is present.
    /// </summary>
    private static async Task<string> PendingRequestIdAsync(HttpClient client, string sessionId, CancellationToken ct)
    {
        using var timeoutSource = new CancellationTokenSource(SettleTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, ct);
        var cancellationToken = linked.Token;
        UnknownPayload[] requests = [];
        while (requests.Length == 0)
        {
            var session = await SessionClient.ReadSessionAsync(client, sessionId, cancellationToken).ConfigureAwait(false);
            requests = session.Events
                .Select(static evt => evt.Payload)
                .OfType<UnknownPayload>()
                .Where(static payload => string.Equals(payload.OriginalKind, "tool_approval_requested", StringComparison.Ordinal))
                .ToArray();
            if (requests.Length == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
        }

        requests.Length.ShouldBe(1, $"session '{sessionId}' must log exactly one tool_approval_requested event");
        var requestId = requests[0].Raw.GetProperty("requestId").GetString();
        return requestId.ShouldNotBeNull();
    }

    private static async Task<HttpResponseMessage> PostDecisionAsync(
        HttpClient client,
        string sessionId,
        string requestId,
        string body,
        string contentType,
        CancellationToken ct)
    {
        using var content = new StringContent(body, Encoding.UTF8, contentType);
        return await client.PostAsync(new Uri($"/sessions/{sessionId}/tool-approvals/{requestId}", UriKind.Relative), content, ct).ConfigureAwait(false);
    }

    private static string?[] EventKinds(Session session) =>
        session.Events
            .Select(static evt => evt.Payload)
            .OfType<UnknownPayload>()
            .Select(static payload => payload.OriginalKind)
            .ToArray();

    /// <summary>A resolver whose answer the test releases, and which reports when the pause asked for it.</summary>
    private sealed class GateResolver : IPermissionResolver
    {
        private readonly TaskCompletionSource<PermissionDecision> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes once the pause has asked for a decision, or fails at the timeout.</summary>
        /// <param name="timeout">How long to wait for the pause to ask.</param>
        /// <param name="cancellationToken">Cancels the wait.</param>
        public Task WaitForEntryAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
            _entered.Task.WaitAsync(timeout, cancellationToken);

        /// <summary>Releases the pending decision.</summary>
        /// <param name="decision">The decision to deliver.</param>
        public void Answer(PermissionDecision decision) => _answer.TrySetResult(decision);

        /// <inheritdoc />
        public Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            return _answer.Task.WaitAsync(cancellationToken);
        }
    }

    /// <summary>A resolver that never answers, so only the pause timeout can end its wait.</summary>
    private sealed class SilentResolver : IPermissionResolver
    {
        /// <inheritdoc />
        public async Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return PermissionDecision.Allow;
        }
    }
}
