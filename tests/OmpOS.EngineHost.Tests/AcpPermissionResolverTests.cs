using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using Shouldly;
using Xunit;

namespace OmpOS.EngineHost.Tests;

/// <summary>
/// The permission resolver of the in-process ACP composition. A scripted ACP peer stands in for <c>omp acp</c>, so no test
/// spawns a process: each prompt asks for one permission, and the peer records the answer the client sends back.
/// </summary>
public sealed class AcpPermissionResolverTests
{
    /// <summary>The tool kind the admitting policy allows. The peer's permission request carries it.</summary>
    private const string AdmittedKind = "execute";

    /// <summary>An admitted request is decided by the resolver: an allow reaches the agent as the allow option.</summary>
    [Fact]
    public async Task AnAdmittedRequestIsGrantedWhenTheResolverAllows()
    {
        var resolver = new RecordingResolver(PermissionDecision.Allow);
        var peer = new ScriptedAcpPeer();
        await using var peerScope = peer.ConfigureAwait(true);
        await using var host = InProcessEngineHost.ForAcp(AdmittingOptions(), resolver, transportFactory: _ => peer);

        var events = await CollectAsync(host, TestContext.Current.CancellationToken);

        resolver.Requests.ShouldHaveSingleItem().CallId.ShouldBe("call-1");
        events.OfType<PermissionRequestEvent>().ShouldHaveSingleItem();
        peer.SelectedOptionId.ShouldBe("allow");
    }

    /// <summary>A denial from the resolver reaches the agent as the reject option, never as a grant.</summary>
    [Fact]
    public async Task AResolverDenialRejectsTheAdmittedRequest()
    {
        var resolver = new RecordingResolver(PermissionDecision.Deny);
        var peer = new ScriptedAcpPeer();
        await using var peerScope = peer.ConfigureAwait(true);
        await using var host = InProcessEngineHost.ForAcp(AdmittingOptions(), resolver, transportFactory: _ => peer);

        var events = await CollectAsync(host, TestContext.Current.CancellationToken);

        resolver.Requests.ShouldHaveSingleItem();
        events.OfType<PermissionRequestEvent>().ShouldHaveSingleItem();
        peer.SelectedOptionId.ShouldBe("reject");
    }

    /// <summary>With no resolver, an admitted request is denied, as it was before the resolver parameter existed.</summary>
    [Fact]
    public async Task WithoutAResolverAnAdmittedRequestIsDenied()
    {
        var peer = new ScriptedAcpPeer();
        await using var peerScope = peer.ConfigureAwait(true);
        await using var host = InProcessEngineHost.ForAcp(AdmittingOptions(), permissionResolver: null, transportFactory: _ => peer);

        var events = await CollectAsync(host, TestContext.Current.CancellationToken);

        events.OfType<PermissionRequestEvent>().ShouldHaveSingleItem();
        peer.SelectedOptionId.ShouldBe("reject");
    }

    /// <summary>Under the default DenyAll policy the resolver is never consulted, so an allow it would give is never granted.</summary>
    [Fact]
    public async Task ADenyAllPolicyNeverConsultsTheResolver()
    {
        var resolver = new RecordingResolver(PermissionDecision.Allow);
        var peer = new ScriptedAcpPeer();
        await using var peerScope = peer.ConfigureAwait(true);
        await using var host = InProcessEngineHost.ForAcp(new AcpClientOptions(), resolver, transportFactory: _ => peer);

        var events = await CollectAsync(host, TestContext.Current.CancellationToken);

        resolver.Requests.ShouldBeEmpty();
        events.OfType<PermissionRequestEvent>().ShouldBeEmpty();
        peer.SelectedOptionId.ShouldBe("reject");
    }

    private static AcpClientOptions AdmittingOptions() => new() { ApprovalPolicy = new AcpApprovalPolicy([AdmittedKind]) };

    private static async Task<List<HarnessEvent>> CollectAsync(InProcessEngineHost host, CancellationToken cancellationToken)
    {
        var events = new List<HarnessEvent>();
        await foreach (var evt in host.SubmitPromptAsync("run the tool", new PromptOptions(), cancellationToken).ConfigureAwait(true))
        {
            events.Add(evt);
        }

        return events;
    }

    /// <summary>A resolver that answers every request with one fixed decision and records each request it was asked.</summary>
    private sealed class RecordingResolver(PermissionDecision decision) : IPermissionResolver
    {
        /// <summary>Gets every request the resolver was asked to decide, in order.</summary>
        public ConcurrentQueue<PermissionRequestEvent> Requests { get; } = new();

        /// <inheritdoc />
        public Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request);
            return Task.FromResult(decision);
        }
    }

    /// <summary>
    /// A scripted ACP peer. It answers <c>initialize</c> and <c>session/new</c>, and for each <c>session/prompt</c> it sends one
    /// <c>session/request_permission</c> for a call of kind <see cref="AdmittedKind"/>. The prompt completes only after the client
    /// answers that permission, and the answer is recorded so the test can read what the agent was told.
    /// </summary>
    private sealed class ScriptedAcpPeer : IAcpTransport
    {
        private const long PermissionRequestId = 900;

        private readonly Channel<ReadOnlyMemory<byte>> _inbound = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly Lock _gate = new();
        private long _promptId;
        private string? _selectedOptionId;

        /// <summary>Gets the option the client selected for the permission request, or null when it selected none.</summary>
        public string? SelectedOptionId
        {
            get
            {
                lock (_gate)
                {
                    return _selectedOptionId;
                }
            }
        }

        /// <inheritdoc />
        public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
        {
            using var document = JsonDocument.Parse(frame);
            var root = document.RootElement;
            if (!root.TryGetProperty("id", out var idElement))
            {
                return Task.CompletedTask;
            }

            var id = idElement.GetInt64();
            if (!root.TryGetProperty("method", out var methodElement))
            {
                // The client's answer to the permission request: the prompt now completes.
                if (id == PermissionRequestId)
                {
                    RecordSelection(root);
                    Enqueue(Result(_promptId, "{\"stopReason\":\"end_turn\"}"));
                }

                return Task.CompletedTask;
            }

            switch (methodElement.GetString())
            {
                case "initialize":
                    Enqueue(Result(id, "{\"protocolVersion\":1,\"agentCapabilities\":{\"loadSession\":false,\"mcpCapabilities\":{},\"promptCapabilities\":{},\"sessionCapabilities\":{}}}"));
                    break;
                case "session/new":
                    Enqueue(Result(id, "{\"sessionId\":\"session-acp\"}"));
                    break;
                case "session/close":
                    Enqueue(Result(id, "{}"));
                    break;
                case "session/prompt":
                    lock (_gate)
                    {
                        _promptId = id;
                    }

                    Enqueue(PermissionRequest);
                    break;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var frame in _inbound.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return frame;
            }
        }

        /// <inheritdoc />
        public Task CloseAsync(CancellationToken cancellationToken)
        {
            _inbound.Writer.TryComplete();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _inbound.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        private void RecordSelection(JsonElement root)
        {
            var outcome = root.GetProperty("result").GetProperty("outcome");
            var selected = outcome.TryGetProperty("optionId", out var optionId) ? optionId.GetString() : null;
            lock (_gate)
            {
                _selectedOptionId = selected;
            }
        }

        private void Enqueue(string frame) => _inbound.Writer.TryWrite(Encoding.UTF8.GetBytes(frame));

        private static string Result(long id, string result) => $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{result}}}";

        /// <summary>The permission request the peer sends for each prompt: id 900, one call of kind <c>execute</c>, an allow and a reject option.</summary>
        private const string PermissionRequest = """{"jsonrpc":"2.0","id":900,"method":"session/request_permission","params":{"sessionId":"session-acp","toolCall":{"toolCallId":"call-1","title":"bash","kind":"execute"},"options":[{"optionId":"allow","name":"Allow","kind":"allow_once"},{"optionId":"reject","name":"Reject","kind":"reject_once"}]}}""";
    }
}
