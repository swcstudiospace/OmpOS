using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Autonogrammer.Api.Harness;
using Autonogrammer.Api.Options;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Harness;

/// <summary>
/// The operator approval policy of the direct run (<c>Harness:Acp:Approval</c>), end to end over HTTP and a scripted omp ACP peer. The defaults deny every
/// permission request; a configured kind becomes a pending approval that the owner's decision, its ceiling and the decision timeout settle. The peer is
/// in-process and scripted; no omp runs.
/// </summary>
public sealed class AcpApprovalPolicyRoundTripTests
{
    private const long PermissionRequestId = 900;
    private const string AcpSessionId = "acp-session";
    private const string ToolTitle = "Run it";
    private const string AllowOptionId = "allow-once";
    private const string RejectOptionId = "reject-once";
    private const string JsonType = "application/json";
    private const string OptionsJson =
        "[{\"optionId\":\"allow-once\",\"name\":\"Allow\",\"kind\":\"allow_once\"},{\"optionId\":\"reject-once\",\"name\":\"Reject\",\"kind\":\"reject_once\"}]";
    private const string InitializeResultJson =
        "{\"protocolVersion\":1,\"agentInfo\":{\"name\":\"omp\",\"title\":\"omp\",\"version\":\"18.12.3\"},\"agentCapabilities\":{\"loadSession\":true,\"mcpCapabilities\":{\"http\":true,\"sse\":true},\"promptCapabilities\":{\"embeddedContext\":true,\"image\":true},\"sessionCapabilities\":{\"list\":{},\"fork\":{},\"resume\":{},\"close\":{}}}}";

    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>With nothing configured every permission request is denied at once: no pending approval, a reject for the engine, and the policy diagnostic in the log.</summary>
    [Fact]
    public async Task DefaultsDenyEverythingAndRecordThePolicyDiagnostic()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new PeerScript("execute");
        using var factory = ApprovalFactory(peer, []);
        using var client = factory.CreateClient();
        var id = await StartPromptAsync(client, ct).ConfigureAwait(true);

        OptionOf(await peer.Answered.Task.WaitAsync(SettleTimeout, ct).ConfigureAwait(true)).ShouldBe(RejectOptionId);
        var completed = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, ct).ConfigureAwait(true);

        EventKinds(completed).ShouldNotContain("tool_approval_requested", StringComparer.Ordinal);
        HasPolicyDiagnostic(completed).ShouldBeTrue();
    }

    /// <summary>A kind outside <c>Kinds</c> is denied at once even though another kind is admitted.</summary>
    [Fact]
    public async Task KindNotInKindsIsDeniedAtOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new PeerScript("execute");
        using var factory = ApprovalFactory(peer, Config(kinds: ["read"], grantable: ["*"]));
        using var client = factory.CreateClient();
        var id = await StartPromptAsync(client, ct).ConfigureAwait(true);

        OptionOf(await peer.Answered.Task.WaitAsync(SettleTimeout, ct).ConfigureAwait(true)).ShouldBe(RejectOptionId);
        var completed = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, ct).ConfigureAwait(true);

        EventKinds(completed).ShouldNotContain("tool_approval_requested", StringComparer.Ordinal);
        HasPolicyDiagnostic(completed).ShouldBeTrue();
    }

    /// <summary>An admitted kind becomes a pending approval whose ceiling is the configured list; an allow reaches the engine and the run completes.</summary>
    [Fact]
    public async Task AdmittedKindBecomesPendingAndAllowReachesTheEngine()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new PeerScript("execute");
        using var factory = ApprovalFactory(peer, Config(kinds: ["execute"], grantable: ["*"]));
        using var client = factory.CreateClient();
        var id = await StartPromptAsync(client, ct).ConfigureAwait(true);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.AwaitingToolApproval, SettleTimeout, ct).ConfigureAwait(true);
        var requestId = await PendingRequestIdAsync(client, id, ct).ConfigureAwait(true);

        var stored = await factory.Services.GetRequiredService<ISessionRepository>().GetToolApprovalAsync(id, requestId, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull().ToolName.ShouldBe(ToolTitle);
        stored.GrantableTools.ShouldBe(["*"]);

        using (var allowed = await PostDecisionAsync(client, id, requestId, "allow", ct).ConfigureAwait(true))
        {
            allowed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        OptionOf(await peer.Answered.Task.WaitAsync(SettleTimeout, ct).ConfigureAwait(true)).ShouldBe(AllowOptionId);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, ct).ConfigureAwait(true);
    }

    /// <summary>A deny reaches the engine as a rejection and the run completes.</summary>
    [Fact]
    public async Task DenyRejectsTheEngineRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new PeerScript("execute");
        using var factory = ApprovalFactory(peer, Config(kinds: ["execute"], grantable: [ToolTitle]));
        using var client = factory.CreateClient();
        var id = await StartPromptAsync(client, ct).ConfigureAwait(true);
        var requestId = await PendingRequestIdAsync(client, id, ct).ConfigureAwait(true);

        using (var denied = await PostDecisionAsync(client, id, requestId, "deny", ct).ConfigureAwait(true))
        {
            denied.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        OptionOf(await peer.Answered.Task.WaitAsync(SettleTimeout, ct).ConfigureAwait(true)).ShouldBe(RejectOptionId);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, ct).ConfigureAwait(true);
    }

    /// <summary>With no decision inside <c>DecisionTimeoutSeconds</c> the request is denied and settled: the engine gets a rejection and the store records the denial.</summary>
    [Fact]
    public async Task NoDecisionWithinTheTimeoutDeniesAndSettles()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new PeerScript("execute");
        using var factory = ApprovalFactory(peer, Config(kinds: ["execute"], grantable: ["*"], timeoutSeconds: 1));
        using var client = factory.CreateClient();
        var id = await StartPromptAsync(client, ct).ConfigureAwait(true);
        var requestId = await PendingRequestIdAsync(client, id, ct).ConfigureAwait(true);

        OptionOf(await peer.Answered.Task.WaitAsync(SettleTimeout, ct).ConfigureAwait(true)).ShouldBe(RejectOptionId);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, ct).ConfigureAwait(true);

        var record = await factory.Services.GetRequiredService<ISessionRepository>().GetToolApprovalAsync(id, requestId, ct).ConfigureAwait(true);
        record.ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Deny);
    }

    /// <summary>
    /// The ceiling bounds an allow: an empty list (the default) and a list that names another tool refuse it with 403 and the request stays pending for a denial;
    /// the tool's own name and <c>*</c> permit it.
    /// </summary>
    [Theory]
    [InlineData("", HttpStatusCode.Forbidden)]
    [InlineData("other-tool", HttpStatusCode.Forbidden)]
    [InlineData(ToolTitle, HttpStatusCode.NoContent)]
    [InlineData("*", HttpStatusCode.NoContent)]
    public async Task CeilingBoundsWhatAnAllowMayGrant(string grantable, HttpStatusCode expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new PeerScript("execute");
        string[] ceiling = grantable.Length == 0 ? [] : [grantable];
        using var factory = ApprovalFactory(peer, Config(kinds: ["execute"], grantable: ceiling));
        using var client = factory.CreateClient();
        var id = await StartPromptAsync(client, ct).ConfigureAwait(true);
        var requestId = await PendingRequestIdAsync(client, id, ct).ConfigureAwait(true);

        using (var allowed = await PostDecisionAsync(client, id, requestId, "allow", ct).ConfigureAwait(true))
        {
            allowed.StatusCode.ShouldBe(expected);
        }

        if (expected == HttpStatusCode.NoContent)
        {
            OptionOf(await peer.Answered.Task.WaitAsync(SettleTimeout, ct).ConfigureAwait(true)).ShouldBe(AllowOptionId);
        }
        else
        {
            (await SessionClient.ReadSessionAsync(client, id, ct).ConfigureAwait(true)).Status.ShouldBe(SessionStatus.AwaitingToolApproval);
            using var denied = await PostDecisionAsync(client, id, requestId, "deny", ct).ConfigureAwait(true);
            denied.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            OptionOf(await peer.Answered.Task.WaitAsync(SettleTimeout, ct).ConfigureAwait(true)).ShouldBe(RejectOptionId);
        }

        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, ct).ConfigureAwait(true);
    }

    private static Dictionary<string, string?> Config(string[] kinds, string[] grantable, int? timeoutSeconds = null)
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var index = 0; index < kinds.Length; index++)
        {
            config[$"Harness:Acp:Approval:Kinds:{index}"] = kinds[index];
        }

        for (var index = 0; index < grantable.Length; index++)
        {
            config[$"Harness:Acp:Approval:GrantableTools:{index}"] = grantable[index];
        }

        if (timeoutSeconds is { } seconds)
        {
            config["Harness:Acp:Approval:DecisionTimeoutSeconds"] = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return config;
    }

    /// <summary>
    /// Builds the API with the ACP harness factory over the scripted peer. The policy and the decision timeout come from the configured
    /// <c>Harness:Acp:Approval</c> keys, as the production registration reads them; the API's own settings supply the ceiling.
    /// </summary>
    private static ApiFactory ApprovalFactory(PeerScript peer, Dictionary<string, string?> config)
    {
        return new ApiFactory(
            config,
            services => services.AddSingleton<IHarnessSessionFactory>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<HarnessOptions>>();
                var approval = options.Value.Acp.Approval;
                return new AcpHarnessSessionFactory(
                    provider.GetRequiredService<OmpAcpClient>(),
                    options,
                    provider.GetRequiredService<ISessionRepository>(),
                    provider.GetRequiredService<SessionService>(),
                    _ => new PeerTransport(peer),
                    approval.CreatePolicy(),
                    approval.DecisionTimeout);
            }));
    }

    private static async Task<string> StartPromptAsync(HttpClient client, CancellationToken ct)
    {
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"run\"}", ct).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        return id;
    }

    private static bool HasPolicyDiagnostic(Session session) =>
        session.Events
            .Select(static evt => evt.Payload)
            .OfType<DiagnosticPayload>()
            .Any(static payload => payload.Message.Contains("Approval policy denied permission request", StringComparison.Ordinal));

    private static string?[] EventKinds(Session session) =>
        session.Events
            .Select(static evt => evt.Payload)
            .OfType<UnknownPayload>()
            .Select(static payload => payload.OriginalKind)
            .ToArray();

    /// <summary>Reads the identifier of the pending tool approval from the session event log, as a UI does, waiting until its single event is present.</summary>
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
        return requests[0].Raw.GetProperty("requestId").GetString().ShouldNotBeNull();
    }

    private static async Task<HttpResponseMessage> PostDecisionAsync(HttpClient client, string sessionId, string requestId, string decision, CancellationToken ct)
    {
        using var content = new StringContent($"{{\"decision\":\"{decision}\"}}", Encoding.UTF8, JsonType);
        var path = $"/sessions/{sessionId}/tool-approvals/{Uri.EscapeDataString(requestId)}";
        return await client.PostAsync(new Uri(path, UriKind.Relative), content, ct).ConfigureAwait(true);
    }

    /// <summary>Reads the outcome option id from a permission answer.</summary>
    private static string OptionOf(byte[] answer) =>
        JsonNode.Parse(answer)!["result"]!["outcome"]!["optionId"]!.GetValue<string>();

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>
    /// The scripted omp ACP peer. It answers the handshake, asks for permission for a tool of the given kind while the prompt runs, and completes
    /// the prompt once the client has answered the permission request.
    /// </summary>
    private sealed class PeerScript(string kind)
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

        private string PermissionRequest() =>
            $"{{\"jsonrpc\":\"2.0\",\"id\":{PermissionRequestId},\"method\":\"session/request_permission\",\"params\":{{\"sessionId\":\"{AcpSessionId}\",\"toolCall\":{{\"toolCallId\":\"call-1\",\"title\":\"{ToolTitle}\",\"kind\":\"{kind}\"}},\"options\":{OptionsJson}}}}}";
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
