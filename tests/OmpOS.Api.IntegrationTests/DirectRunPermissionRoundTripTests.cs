using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Api.Harness;
using OmpOS.Api.Options;
using OmpOS.Contracts.Errors;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Sessions;
using OmpOS.Workflows;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// The direct run's ACP permission round trip. A scripted omp ACP peer asks for permission while a prompt runs. The run
/// registers that request in the session store, and a decision recorded through the endpoint, or the decision timeout, answers
/// the ACP request. Every path without an allow decision denies. The peer is in-process and scripted; no omp runs.
/// </summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The ACP session owns and disposes its scripted transport.")]
public sealed class DirectRunPermissionRoundTripTests
{
    private const long PermissionRequestId = 900;
    private const string AcpSessionId = "acp-session";
    private const string RunId = "run-direct";
    private const string ToolTitle = "Run it";
    private const string AllowOptionId = "allow-once";
    private const string RejectOptionId = "reject-once";
    private const string JsonType = "application/json";
    private const string OptionsJson =
        "[{\"optionId\":\"allow-once\",\"name\":\"Allow\",\"kind\":\"allow_once\"},{\"optionId\":\"reject-once\",\"name\":\"Reject\",\"kind\":\"reject_once\"}]";
    // Capabilities as the real adapter sends them. authMethods is omitted: this peer's harness configures no AuthMethodId.
    private const string InitializeResultJson =
        "{\"protocolVersion\":1,\"agentInfo\":{\"name\":\"omp\",\"title\":\"omp\",\"version\":\"18.12.3\"},\"agentCapabilities\":{\"loadSession\":true,\"mcpCapabilities\":{\"http\":true,\"sse\":true},\"promptCapabilities\":{\"embeddedContext\":true,\"image\":true},\"sessionCapabilities\":{\"list\":{},\"fork\":{},\"resume\":{},\"close\":{}}}}";

    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DecisionTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ShortDecisionTimeout = TimeSpan.FromSeconds(1);

    /// <summary>A deny recorded through the endpoint reaches the ACP request as a rejection, the run completes, and a repeated decision is a 409.</summary>
    [Fact]
    public async Task DenyFromUiAnswersAcpRequestAndRunCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new PeerScript();
        using var factory = ApprovalFactory(peer, DecisionTimeout);
        using var client = factory.CreateClient();
        var id = await StartAwaitingApprovalAsync(client, ct).ConfigureAwait(true);
        var requestId = await PendingRequestIdAsync(client, id, ct).ConfigureAwait(true);

        using (var denied = await PostDecisionAsync(client, id, requestId, "{\"decision\":\"deny\"}", ct).ConfigureAwait(true))
        {
            denied.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var answer = await peer.Answered.Task.WaitAsync(SettleTimeout, ct).ConfigureAwait(true);
        OptionOf(answer).ShouldBe(RejectOptionId);
        var completed = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, ct).ConfigureAwait(true);
        completed.Status.ShouldBe(SessionStatus.Completed);

        using var repeat = await PostDecisionAsync(client, id, requestId, "{\"decision\":\"deny\"}", ct).ConfigureAwait(true);
        repeat.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// An allow on a direct run is refused by the request's ceiling, which the run sets to none: 403, and the request stays pending.
    /// The deny that follows still reaches the ACP request, so the request is never left unanswered.
    /// </summary>
    [Fact]
    public async Task AllowOnDirectRunIsForbiddenByCeilingAndDenyStillAnswersAcp()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new PeerScript();
        using var factory = ApprovalFactory(peer, DecisionTimeout);
        using var client = factory.CreateClient();
        var id = await StartAwaitingApprovalAsync(client, ct).ConfigureAwait(true);
        var requestId = await PendingRequestIdAsync(client, id, ct).ConfigureAwait(true);

        using (var forbidden = await PostDecisionAsync(client, id, requestId, "{\"decision\":\"allow\"}", ct).ConfigureAwait(true))
        {
            forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        var stillAwaiting = await SessionClient.ReadSessionAsync(client, id, ct).ConfigureAwait(true);
        stillAwaiting.Status.ShouldBe(SessionStatus.AwaitingToolApproval);

        using (var denied = await PostDecisionAsync(client, id, requestId, "{\"decision\":\"deny\"}", ct).ConfigureAwait(true))
        {
            denied.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        OptionOf(await peer.Answered.Task.WaitAsync(SettleTimeout, ct).ConfigureAwait(true)).ShouldBe(RejectOptionId);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, ct).ConfigureAwait(true);
    }

    /// <summary>With no decision within the bound, the request is denied: the ACP request is answered with a rejection and the store records a denial.</summary>
    [Fact]
    public async Task NoDecisionWithinBoundDeniesAcpRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new PeerScript();
        using var factory = ApprovalFactory(peer, ShortDecisionTimeout);
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        using (var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"run\"}", ct).ConfigureAwait(true))
        {
            accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        var requestId = await PendingRequestIdAsync(client, id, ct).ConfigureAwait(true);

        OptionOf(await peer.Answered.Task.WaitAsync(SettleTimeout, ct).ConfigureAwait(true)).ShouldBe(RejectOptionId);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, ct).ConfigureAwait(true);
        var record = await factory.Services.GetRequiredService<ISessionRepository>().GetToolApprovalAsync(id, requestId, ct).ConfigureAwait(true);
        record.ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Deny);
    }

    /// <summary>
    /// A request the approval policy forbids is answered reject at once and never becomes an approval: the run completes without
    /// waiting, and the event log holds no tool_approval_requested event.
    /// </summary>
    [Fact]
    public async Task PolicyDeniedRequestIsRejectedWithoutPendingApproval()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new PeerScript();
        using var factory = ApprovalFactory(peer, DecisionTimeout, new AcpApprovalPolicy(["read"]));
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        using (var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"run\"}", ct).ConfigureAwait(true))
        {
            accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        OptionOf(await peer.Answered.Task.WaitAsync(SettleTimeout, ct).ConfigureAwait(true)).ShouldBe(RejectOptionId);
        var completed = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, ct).ConfigureAwait(true);
        EventKinds(completed).ShouldNotContain("tool_approval_requested", StringComparer.Ordinal);
    }

    /// <summary>
    /// With no decision inside the window, the request is denied and settled before the agent is answered. A later allow finds the
    /// session no longer awaiting approval: 409, and the stored decision stays deny.
    /// </summary>
    [Fact]
    public async Task DecisionAfterWindowIsConflictAndStoredDenialStays()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new PeerScript();
        using var factory = ApprovalFactory(peer, ShortDecisionTimeout);
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        using (var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"run\"}", ct).ConfigureAwait(true))
        {
            accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        var requestId = await PendingRequestIdAsync(client, id, ct).ConfigureAwait(true);
        var repository = factory.Services.GetRequiredService<ISessionRepository>();

        OptionOf(await peer.Answered.Task.WaitAsync(SettleTimeout, ct).ConfigureAwait(true)).ShouldBe(RejectOptionId);
        var atAnswer = await repository.GetToolApprovalAsync(id, requestId, ct).ConfigureAwait(true);
        atAnswer.ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Deny);

        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, ct).ConfigureAwait(true);
        using (var late = await PostDecisionAsync(client, id, requestId, "{\"decision\":\"allow\"}", ct).ConfigureAwait(true))
        {
            late.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        var stored = await repository.GetToolApprovalAsync(id, requestId, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Deny);
    }

    private static string?[] EventKinds(Session session) =>
        session.Events
            .Select(static evt => evt.Payload)
            .OfType<UnknownPayload>()
            .Select(static payload => payload.OriginalKind)
            .ToArray();

    /// <summary>
    /// An allow recorded for a request whose ceiling permits its tool reaches the ACP request as an allow. This exercises the grant
    /// path of the store-backed resolver with a ceiling. The direct run registers no ceiling, so the run cannot reach this path yet.
    /// </summary>
    [Fact]
    public async Task AllowWithinCeilingReachesAcpRequestAsAllow()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var stored = await sessions.CreateAsync("owner", ct).ConfigureAwait(true);
        await sessions.SubmitPromptAsync(stored.Id, "run", ct).ConfigureAwait(true);
        var requestId = PermissionRequestKey(RunId);
        await sessions.RequestToolApprovalAsync(stored.Id, requestId, ToolTitle, ct, new HashSet<string>(StringComparer.Ordinal) { ToolTitle }).ConfigureAwait(true);
        await sessions.ResolveToolApprovalAsync(stored.Id, requestId, true, ct).ConfigureAwait(true);

        var peer = new PeerScript();
        var resolver = new SessionPermissionResolver(repository, sessions, stored.Id, RunId, DecisionTimeout, SessionPermissionResolver.DefaultRegistrationTimeout);
        var events = await PromptAsync(peer, resolver, ct).ConfigureAwait(true);

        OptionOf(await peer.Answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true)).ShouldBe(AllowOptionId);
        events.Last().ShouldBeOfType<CompletedEvent>();
    }

    /// <summary>A request the run never registers is denied at the ACP request once the registration bound lapses.</summary>
    [Fact]
    public async Task RequestNeverRegisteredDeniesAcpRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var stored = await sessions.CreateAsync("owner", ct).ConfigureAwait(true);
        var peer = new PeerScript();
        var resolver = new SessionPermissionResolver(repository, sessions, stored.Id, RunId, DecisionTimeout, TimeSpan.FromMilliseconds(200));

        _ = await PromptAsync(peer, resolver, ct).ConfigureAwait(true);

        OptionOf(await peer.Answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true)).ShouldBe(RejectOptionId);
        (await repository.GetToolApprovalAsync(stored.Id, PermissionRequestKey(RunId), ct).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>
    /// A request that no run registers within the registration bound is settled before the agent is answered. It is stored as
    /// denied, so it is not pending, and a DiagnosticEvent records the reason. The agent gets a reject, and a later allow is refused.
    /// </summary>
    [Fact]
    public async Task UnregisteredRequestIsSettledRejectedAtRegistrationBound()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var stored = await sessions.CreateAsync("owner", ct).ConfigureAwait(true);
        await sessions.SubmitPromptAsync(stored.Id, "run", ct).ConfigureAwait(true);
        var requestId = PermissionRequestKey(RunId);
        var peer = new PeerScript();
        var resolver = new SessionPermissionResolver(repository, sessions, stored.Id, RunId, DecisionTimeout, TimeSpan.FromMilliseconds(200));

        _ = await PromptAsync(peer, resolver, ct).ConfigureAwait(true);

        OptionOf(await peer.Answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true)).ShouldBe(RejectOptionId);
        var record = await repository.GetToolApprovalAsync(stored.Id, requestId, ct).ConfigureAwait(true);
        record.ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Deny);
        var session = (await repository.GetAsync(stored.Id, ct).ConfigureAwait(true)).ShouldNotBeNull();
        session.Status.ShouldBe(SessionStatus.Running);
        session.Events.Select(static evt => evt.Payload).OfType<DiagnosticPayload>().ShouldHaveSingleItem().Message.ShouldContain("registration bound");

        var late = await Should.ThrowAsync<SessionException>(() => sessions.ResolveToolApprovalAsync(stored.Id, requestId, true, ct)).ConfigureAwait(true);
        late.Error.Code.ShouldBe(ErrorCode.InvalidTransition);
        (await repository.GetToolApprovalAsync(stored.Id, requestId, ct).ConfigureAwait(true)).ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Deny);
    }

    /// <summary>
    /// A run whose permission request is registered after the resolver settled it as denied at the registration bound. The
    /// registration is refused, the run does not fail, and the turn ends through the normal path with one denial in the log.
    /// </summary>
    [Fact]
    public async Task LateRegistrationOfSettledRequestEndsRunWithOneDenial()
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = new ScriptedRunSession(new PermissionRequestEvent("call-1", ToolTitle, "{}", PermissionRequestId.ToString(CultureInfo.InvariantCulture)));
        using var factory = new OmpApiFactory(configureServices: services => services.AddSingleton<IHarnessSessionFactory>(new ScriptedRunFactory(harness)));
        var container = factory.Services;
        var sessions = container.GetRequiredService<SessionService>();
        var repository = container.GetRequiredService<ISessionRepository>();
        var stored = await sessions.CreateAsync("owner", ct).ConfigureAwait(true);
        await sessions.SubmitPromptAsync(stored.Id, "run", ct).ConfigureAwait(true);

        // The ACP resolver settles the request at the registration bound, before the run registers it.
        var resolver = new SessionPermissionResolver(repository, sessions, stored.Id, RunId, DecisionTimeout, TimeSpan.FromMilliseconds(200));
        _ = await resolver.ResolveAsync(new PermissionRequestEvent("call-1", ToolTitle, "{}", PermissionRequestId.ToString(CultureInfo.InvariantCulture)), ct).ConfigureAwait(true);

        var input = new SessionRunInput
        {
            RunId = RunId,
            SessionId = stored.Id,
            SandboxId = "sandbox-late",
            CorrelationId = $"{stored.Id}/turn-1",
            Prompt = "run",
            ResumeFromSeq = 0,
            IdempotencyKey = "key-late",
        };
        _ = await container.GetRequiredService<ISessionRunActivities>().RunPromptAsync(input, static _ => { }, ct).ConfigureAwait(true);

        var session = (await repository.GetAsync(stored.Id, ct).ConfigureAwait(true)).ShouldNotBeNull();
        session.Status.ShouldBe(SessionStatus.Completed);
        EventKinds(session).Count(static kind => string.Equals(kind, "tool_approval_denied", StringComparison.Ordinal)).ShouldBe(1);
    }

    /// <summary>A harness session that asks for one permission and then completes its turn. The run is its only consumer.</summary>
    private sealed class ScriptedRunSession(PermissionRequestEvent request) : IOmpSession
    {
        /// <inheritdoc />
        public Task CreateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return request;
            yield return new CompletedEvent("end_turn");
        }

        /// <inheritdoc />
        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public HarnessState State => HarnessState.Ready;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Hands every run the same scripted harness session.</summary>
    private sealed class ScriptedRunFactory(IOmpSession session) : IHarnessSessionFactory
    {
        /// <inheritdoc />
        public IOmpSession CreateSession(string prompt) => session;

        /// <inheritdoc />
        public IOmpSession CreateSession(string prompt, string sessionId, string runId) => session;
    }

    /// <summary>Builds the API with the ACP harness factory over the scripted peer. The default policy admits execute requests.</summary>
    private static OmpApiFactory ApprovalFactory(PeerScript peer, TimeSpan decisionTimeout, AcpApprovalPolicy? policy = null)
    {
        var transport = new PeerTransport(peer);
        return new OmpApiFactory(configureServices: services => services.AddSingleton<IHarnessSessionFactory>(provider => new AcpHarnessSessionFactory(
            provider.GetRequiredService<OmpAcpClient>(),
            Microsoft.Extensions.Options.Options.Create(new HarnessOptions { Adapter = HarnessOptions.AcpAdapter }),
            provider.GetRequiredService<ISessionRepository>(),
            provider.GetRequiredService<SessionService>(),
            _ => transport,
            policy ?? new AcpApprovalPolicy(["execute"]),
            decisionTimeout)));
    }

    /// <summary>Runs one prompt on a scripted ACP session whose permission resolver is <paramref name="resolver"/>, and returns its events.</summary>
    private static async Task<List<HarnessEvent>> PromptAsync(PeerScript peer, IPermissionResolver resolver, CancellationToken ct)
    {
        var transport = new PeerTransport(peer);
        var client = new OmpAcpClient(_ => transport, resolver, DecisionTimeout);
        var session = client.CreateSession(new AcpClientOptions { ApprovalPolicy = new AcpApprovalPolicy(["execute"]) });
        await using (session.ConfigureAwait(false))
        {
            await session.CreateAsync(ct).ConfigureAwait(true);
            var events = new List<HarnessEvent>();
            await foreach (var evt in session.SubmitPromptAsync("run", new PromptOptions { Steer = false }, ct).ConfigureAwait(true))
            {
                events.Add(evt);
            }

            return events;
        }
    }

    private static async Task<string> StartAwaitingApprovalAsync(HttpClient client, CancellationToken ct)
    {
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"run\"}", ct).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.AwaitingToolApproval, SettleTimeout, ct).ConfigureAwait(true);
        return id;
    }

    /// <summary>
    /// Reads the identifier of the pending tool approval from the session event log, as a UI does. The event is appended after the
    /// session status is saved, so this waits until that single event is present.
    /// </summary>
    private static async Task<string> PendingRequestIdAsync(HttpClient client, string sessionId, CancellationToken ct)
    {
        using var timeoutSource = new CancellationTokenSource(SettleTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, ct);
        var cancellationToken = linked.Token;
        UnknownPayload[] requests = [];
        while (requests.Length == 0)
        {
            var session = await SessionClient.ReadSessionAsync(client, sessionId, cancellationToken).ConfigureAwait(true);
            requests = session.Events
                .Select(static evt => evt.Payload)
                .OfType<UnknownPayload>()
                .Where(static payload => string.Equals(payload.OriginalKind, "tool_approval_requested", StringComparison.Ordinal))
                .ToArray();
            if (requests.Length == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(true);
            }
        }

        requests.Length.ShouldBe(1, $"session '{sessionId}' must log exactly one tool_approval_requested event");
        var requestId = requests[0].Raw.GetProperty("requestId").GetString();
        return requestId.ShouldNotBeNull();
    }

    private static async Task<HttpResponseMessage> PostDecisionAsync(HttpClient client, string sessionId, string requestId, string body, CancellationToken ct)
    {
        using var content = new StringContent(body, Encoding.UTF8, JsonType);
        var path = $"/sessions/{sessionId}/tool-approvals/{Uri.EscapeDataString(requestId)}";
        return await client.PostAsync(new Uri(path, UriKind.Relative), content, ct).ConfigureAwait(true);
    }

    /// <summary>The request identifier the run registers for the scripted permission request: the run identifier scopes the ACP id.</summary>
    private static string PermissionRequestKey(string runId) => $"{runId}:{PermissionRequestId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Reads the outcome option id from a permission answer.</summary>
    private static string OptionOf(byte[] answer) =>
        JsonNode.Parse(answer)!["result"]!["outcome"]!["optionId"]!.GetValue<string>();

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>
    /// The scripted omp ACP peer. It answers the handshake, asks for permission while the prompt runs, and completes the prompt
    /// once the client has answered the permission request.
    /// </summary>
    private sealed class PeerScript
    {
        private long _promptId;

        /// <summary>Gets a task that completes with the client's answer to the permission request.</summary>
        public TaskCompletionSource<byte[]> Answered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Answers one frame the client sent.</summary>
        /// <param name="frame">The frame the client sent.</param>
        /// <returns>The frames the peer sends back.</returns>
        public IEnumerable<byte[]> Reply(byte[] frame)
        {
            var node = JsonNode.Parse(frame)!;
            var method = node["method"]?.GetValue<string>();
            var id = node["id"]?.GetValue<long>() ?? -1;
            if (method is null)
            {
                if (id == PermissionRequestId)
                {
                    Answered.TrySetResult(frame);
                    return [Utf8(Response(_promptId, "{\"stopReason\":\"end_turn\"}"))];
                }

                return [];
            }

            if (string.Equals(method, "initialize", StringComparison.Ordinal))
            {
                return [Utf8(Response(id, InitializeResultJson))];
            }

            if (string.Equals(method, "session/new", StringComparison.Ordinal))
            {
                return [Utf8(Response(id, $"{{\"sessionId\":\"{AcpSessionId}\"}}"))];
            }

            if (string.Equals(method, "session/prompt", StringComparison.Ordinal))
            {
                _promptId = id;
                return [Utf8(PermissionRequest())];
            }

            return [Utf8(Response(id, "{}"))];
        }

        private static string Response(long id, string result) => $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{result}}}";

        private static string PermissionRequest() =>
            $"{{\"jsonrpc\":\"2.0\",\"id\":{PermissionRequestId},\"method\":\"session/request_permission\",\"params\":{{\"sessionId\":\"{AcpSessionId}\",\"toolCall\":{{\"toolCallId\":\"call-1\",\"title\":\"{ToolTitle}\",\"kind\":\"execute\"}},\"options\":{OptionsJson}}}}}";
    }

    /// <summary>An in-memory transport whose peer is <see cref="PeerScript"/>. Every sent frame is answered by the peer.</summary>
    private sealed class PeerTransport(PeerScript peer) : IAcpTransport
    {
        private readonly Channel<byte[]> _inbound = Channel.CreateUnbounded<byte[]>();

        /// <inheritdoc />
        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var reply in peer.Reply(frame.ToArray()))
            {
                _ = _inbound.Writer.TryWrite(reply);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (true)
            {
                if (!await _inbound.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new DisconnectedHarnessException("The scripted peer closed.", "scripted");
                }

                while (_inbound.Reader.TryRead(out var frame))
                {
                    yield return frame;
                }
            }
        }

        /// <inheritdoc />
        public Task CloseAsync(CancellationToken cancellationToken)
        {
            _ = _inbound.Writer.TryComplete();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync() => await CloseAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
