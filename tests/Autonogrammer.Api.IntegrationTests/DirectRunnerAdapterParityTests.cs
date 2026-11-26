using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Autonogrammer.Api.Harness;
using Autonogrammer.Api.IntegrationTests.Harness;
using Autonogrammer.Api.Options;
using Autonogrammer.Contracts.Runs;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Sessions;
using Autonogrammer.Worktrees;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Phase 83 Task 3 fixture proof (RPC-83-2): one scripted session driven through
/// <c>DirectSessionRunner</c> on each local adapter (<c>Harness:Adapter=Acp</c> over a scripted
/// <c>IAcpTransport</c> seam, <c>=Rpc</c> over the RPC factory's scripted session seam) records the
/// same run state — Completed, the same reply text. Replay/scripted only (OD-07): no omp process
/// is spawned and no live model turn runs. The ACP peer frames below are synthesised minimal
/// shapes (initialize/session-new/prompt), not captured omp output.
/// </summary>
public sealed class DirectRunnerAdapterParityTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "autonogrammer-direct-parity-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Root => Path.Combine(_base, "workspaces");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_base))
        {
            Directory.Delete(_base, recursive: true);
        }
    }

    /// <summary>One run through the direct runner completes with the same status and reply on both adapters.</summary>
    /// <param name="adapter">The configured adapter name.</param>
    [Theory]
    [InlineData(HarnessOptions.AcpAdapter)]
    [InlineData(HarnessOptions.RpcAdapter)]
    public async Task RunThroughDirectRunner_CompletesIdenticallyOnBothAdapters(string adapter)
    {
        IHarnessSessionFactory harnessFactory = string.Equals(adapter, HarnessOptions.RpcAdapter, StringComparison.Ordinal) ? CreateRpcFactory() : CreateAcpFactory();
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Harness:Adapter"] = adapter,
            ["Harness:Rpc:WorkspaceRoot"] = Root,
            ["Harness:Acp:WorkspaceRoot"] = Root,
        };
        using var factory = new ApiFactory(config, services => services.AddSingleton<IHarnessSessionFactory>(harnessFactory));
        using var client = factory.CreateClient();
        var sessionId = await SessionClient.CreateSessionAsync(client, Token).ConfigureAwait(true);

        var accepted = await CloudRunClient.CreateRunAsync(client, sessionId, "hello", Token).ConfigureAwait(true);
        var settled = await WaitForRunAsync(client, sessionId, accepted.Id).ConfigureAwait(true);
        settled.Status.ShouldBe(RunStatus.Completed);
        var output = await CloudRunClient.GetOutputAsync(client, sessionId, accepted.Id, Token).ConfigureAwait(true);
        output.Status.ShouldBe(RunStatus.Completed);
        var transcript = output.Transcript.ShouldNotBeNull();
        transcript.Messages.ShouldHaveSingleItem().Content.ShouldBe("hello");
        transcript.Events.ShouldNotBeEmpty();
    }

    private OmpRpcHarnessSessionFactory CreateRpcFactory() => new(
        Microsoft.Extensions.Options.Options.Create(new HarnessOptions
        {
            Adapter = HarnessOptions.RpcAdapter,
            Rpc = new HarnessOptions.RpcSettings { OmpPath = "/opt/omp", WorkspaceRoot = Root },
        }),
        static _ => new ParityScriptedSession(),
        Microsoft.Extensions.Options.Options.Create(new WorktreesOptions()),
        TestHostEnvironment.Development);

    private AcpHarnessSessionFactory CreateAcpFactory()
    {
        var repository = new InMemorySessionRepository();
        Func<AcpClientOptions, IAcpTransport> transportFactory = static _ => new ParityScriptedAcpTransport();
        return new AcpHarnessSessionFactory(
            new OmpAcpClient(transportFactory),
            Microsoft.Extensions.Options.Options.Create(new HarnessOptions
            {
                Adapter = HarnessOptions.AcpAdapter,
                Acp = new HarnessOptions.AcpSettings { OmpPath = "/opt/omp", WorkspaceRoot = Root },
            }),
            repository,
            new SessionService(repository, TimeProvider.System),
            transportFactory,
            new AcpApprovalPolicy([]),
            AcpHarnessSessionFactory.PermissionDecisionTimeout,
            Microsoft.Extensions.Options.Options.Create(new WorktreesOptions()),
            TestHostEnvironment.Development);
    }

    private static async Task<Run> WaitForRunAsync(HttpClient client, string sessionId, string runId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, Token);
        while (true)
        {
            var run = await CloudRunClient.GetRunAsync(client, sessionId, runId, linked.Token).ConfigureAwait(true);
            if (run.Status is RunStatus.Completed or RunStatus.Failed or RunStatus.Cancelled)
            {
                return run;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), linked.Token).ConfigureAwait(true);
        }
    }

    /// <summary>A scripted session: one text delta echoing the prompt and a completion, as the runner consumes from any transport.</summary>
    private sealed class ParityScriptedSession : IOmpSession
    {
        public HarnessState State => HarnessState.Ready;

        public IReadOnlyDictionary<string, string> AdvertisedCapabilities => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["transport"] = "scripted",
        };

        public Task CreateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
            string prompt,
            PromptOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield return new TextDeltaEvent("scripted: " + prompt);
            yield return new CompletedEvent("end_turn");
        }
    }

    /// <summary>
    /// Minimal scripted ACP peer (synthesised frames, not captured omp output): answers
    /// <c>initialize</c>, <c>authenticate</c> and <c>session/new</c>, turns <c>session/prompt</c> into one
    /// text chunk plus an <c>end_turn</c> result, and answers <c>session/close</c>.
    /// </summary>
    private sealed class ParityScriptedAcpTransport : IAcpTransport
    {
        private const string SessionId = "parity-session";

        private readonly Channel<byte[]> _inbound = Channel.CreateUnbounded<byte[]>();

        public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
        {
            foreach (byte[] reply in Respond(frame.ToArray()))
            {
                _ = _inbound.Writer.TryWrite(reply);
            }

            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (await _inbound.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (_inbound.Reader.TryRead(out byte[]? frame))
                {
                    yield return frame;
                }
            }
        }

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            _ = _inbound.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static IEnumerable<byte[]> Respond(byte[] frame)
        {
            JsonObject node = JsonNode.Parse(frame)!.AsObject();
            string? method = node["method"]?.GetValue<string>();
            if (method is null || node["id"] is not { } idNode)
            {
                yield break;
            }

            long id = idNode.GetValue<long>();
            if (string.Equals(method, "session/prompt", StringComparison.Ordinal))
            {
                yield return Utf8("{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{\"sessionId\":\"" + SessionId + "\",\"update\":{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"scripted: hello\"}}}}");
                yield return Result(id, "{\"stopReason\":\"end_turn\"}");
            }
            else
            {
                yield return Result(id, method switch
                {
                    "initialize" => """{"protocolVersion":1,"agentInfo":{"name":"omp","title":"omp","version":"19.2.1"},"authMethods":[{"id":"agent","name":"Use existing local credentials","description":"Authenticate via the provider keys/OAuth state already configured under ~/.omp."}],"agentCapabilities":{"loadSession":true,"mcpCapabilities":{"http":true,"sse":true},"promptCapabilities":{"embeddedContext":true,"image":true},"sessionCapabilities":{"list":{},"fork":{},"resume":{},"close":{}}},"_meta":{"steering":{"supported":true}}}""",
                    "session/new" => "{\"sessionId\":\"" + SessionId + "\",\"configOptions\":[]}",
                    _ => "{}",
                });
            }
        }

        private static byte[] Result(long id, string result) =>
            Utf8("{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":" + result + "}");

        private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
    }
}
