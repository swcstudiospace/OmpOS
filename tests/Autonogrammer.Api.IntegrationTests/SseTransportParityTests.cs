using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Autonogrammer.Api.Harness;
using Autonogrammer.Api.IntegrationTests.Harness;
using Autonogrammer.Api.Options;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Sessions;
using Autonogrammer.Worktrees;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Phase 83 Task 5 (RPC-83-4): one identical scripted event sequence over each local adapter —
/// <c>Harness:Adapter=Acp</c> (real <c>AcpHarnessSessionFactory</c> over a scripted
/// <c>IAcpTransport</c>) and <c>=Rpc</c> (real <c>OmpRpcHarnessSessionFactory</c> over its
/// scripted session seam) — yields byte-equal SSE frames from <c>EventStreamEndpoints</c> and the
/// same <c>Last-Event-ID</c> resume point. Frame citations (one per scripted frame, replay only,
/// OD-07 — no omp process, no live turn): ACP frames are the <c>session/update</c> shapes the
/// contract suite scripts (<c>tests/Autonogrammer.Harness.ContractTests/ScriptedAcpTransport.cs</c>
/// <c>CompletePrompt</c>: <c>agent_message_chunk</c> "Hel", <c>agent_thought_chunk</c> "hmm",
/// <c>tool_call</c> t1/read, <c>tool_call_update</c> completed, <c>agent_message_chunk</c> "lo",
/// prompt result <c>end_turn</c>); the RPC seam yields the identical <c>HarnessEvent</c> sequence
/// (<c>TextDeltaEvent</c>, <c>ThoughtEvent</c>, <c>ToolCallEvent</c>, <c>ToolCallUpdateEvent</c>,
/// <c>TextDeltaEvent</c>, <c>CompletedEvent</c>), as <c>TransportParityScenarios</c> asserts over
/// both scripted transports. Bytes are compared on the captured stream, never on a re-serialised
/// model, and no normaliser is applied.
/// What the byte-equality proves (OQ-529): serializer identity given identical <c>HarnessEvent</c>s
/// — i.e. that <c>HarnessEventProjection</c>/<c>EventStreamEndpoints</c> emits the same SSE bytes
/// when both adapters hand it the same event sequence. It does NOT prove end-to-end mapper parity:
/// production <c>RpcEventMapper</c> embeds the raw RPC frame (<c>frame.GetRawText()</c>) and
/// production <c>AcpWireMapping</c> embeds the raw ACP update (<c>update.GetRawText()</c>) in
/// <c>PayloadJson</c>, so production SSE <c>PayloadJson</c> diverges by construction for tool events.
/// This test's RPC scripted seam deliberately hand-aligns its <c>PayloadJson</c> to the ACP shape
/// so the comparison exercises only the serializer; <c>AcpTaskToolActivitySource</c> parses
/// <c>title</c>/<c>kind</c>/<c>rawInput</c> out of <c>PayloadJson</c>, so tool-activity parsing works
/// only for ACP-shaped <c>PayloadJson</c> (see OQ-529, Linear SPE-37896). No assertion change.
/// </summary>
public sealed class SseTransportParityTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "autonogrammer-sse-parity-" + Guid.NewGuid().ToString("N"));

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

    /// <summary>The captured SSE bytes are identical across adapters, and resume from the same id yields the same bytes.</summary>
    [Fact]
    public async Task SseBytes_AreByteEqualAcrossAdapters_AndResumePointMatches()
    {
        byte[] acpBytes = await CaptureStreamAsync(HarnessOptions.AcpAdapter).ConfigureAwait(true);
        byte[] rpcBytes = await CaptureStreamAsync(HarnessOptions.RpcAdapter).ConfigureAwait(true);

        rpcBytes.ShouldBe(acpBytes);

        List<string> ids = EventIds(Encoding.UTF8.GetString(acpBytes));
        ids.Count.ShouldBeGreaterThan(3);
        string resumeAfter = ids[2];

        byte[] acpResumed = await CaptureStreamAsync(HarnessOptions.AcpAdapter, resumeAfter).ConfigureAwait(true);
        byte[] rpcResumed = await CaptureStreamAsync(HarnessOptions.RpcAdapter, resumeAfter).ConfigureAwait(true);
        rpcResumed.ShouldBe(acpResumed);
        List<string> resumedIds = EventIds(Encoding.UTF8.GetString(acpResumed));
        resumedIds.First().ShouldBe((long.Parse(ids[2], CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture));

        // A one-byte mutation of the captured stream breaks the comparison: the assertion above is
        // a real byte compare, not a model round-trip that would survive it.
        byte[] mutated = acpResumed.ToArray();
        mutated[mutated.Length / 2] ^= 0x01;
        mutated.ShouldNotBe(rpcResumed);
    }

    private static List<string> EventIds(string sseText)
    {
        var ids = new List<string>();
        foreach (string line in sseText.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("id: ", StringComparison.Ordinal))
            {
                ids.Add(trimmed[4..]);
            }
        }

        return ids;
    }


    private async Task<byte[]> CaptureStreamAsync(string adapter, string? lastEventId = null)
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

        using var accepted = await SessionClient.PostPromptAsync(client, sessionId, "{\"prompt\":\"stream me\"}", Token).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, Token);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/sessions/{sessionId}/events", UriKind.Relative));
        if (lastEventId is not null)
        {
            request.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId);
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var content = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(true);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, linked.Token).ConfigureAwait(true);
        return buffer.ToArray();
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

    /// <summary>The RPC-side scripted session: exactly the HarnessEvent sequence the ACP transport below scripts on the wire. Tool payloads carry the same raw update JSON the ACP mapper sets as <c>PayloadJson</c> (<c>AcpWireMapping</c> uses <c>update.GetRawText()</c>), because <c>HarnessEventProjection</c> serialises <c>PayloadJson</c> verbatim into SSE — a placeholder such as <c>{}</c> would diverge byte-wise even though the adapters serialise identically.</summary>
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
            yield return new TextDeltaEvent("Hel");
            yield return new ThoughtEvent("hmm");
            yield return new ToolCallEvent("t1", "read", """{"sessionUpdate":"tool_call","toolCallId":"t1","title":"read","kind":"read","status":"pending"}""");
            yield return new ToolCallUpdateEvent("t1", "completed", """{"sessionUpdate":"tool_call_update","toolCallId":"t1","status":"completed"}""");
            yield return new TextDeltaEvent("lo");
            yield return new CompletedEvent("end_turn");
        }
    }

    /// <summary>
    /// Minimal scripted ACP peer (synthesised frames, not captured omp output): answers
    /// <c>initialize</c>, <c>authenticate</c> and <c>session/new</c>, turns <c>session/prompt</c> into
    /// the frame sequence cited in the class doc, and answers <c>session/close</c>.
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
                yield return Update("""{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"Hel"}}""");
                yield return Update("""{"sessionUpdate":"agent_thought_chunk","content":{"type":"text","text":"hmm"}}""");
                yield return Update("""{"sessionUpdate":"tool_call","toolCallId":"t1","title":"read","kind":"read","status":"pending"}""");
                yield return Update("""{"sessionUpdate":"tool_call_update","toolCallId":"t1","status":"completed"}""");
                yield return Update("""{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"lo"}}""");
                yield return Result(id, """{"stopReason":"end_turn"}""");
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

        private static byte[] Update(string update) =>
            Utf8("{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{\"sessionId\":\"" + SessionId + "\",\"update\":" + update + "}}");

        private static byte[] Result(long id, string result) =>
            Utf8("{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":" + result + "}");

        private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
    }
}
