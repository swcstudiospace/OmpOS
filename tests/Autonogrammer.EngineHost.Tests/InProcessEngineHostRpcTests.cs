using System.Text.Json.Nodes;
using System.Threading.Channels;
using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Rpc;
using Shouldly;
using Xunit;

namespace Autonogrammer.EngineHost.Tests;

/// <summary>
/// Phase 80 Task 8: the single-session RPC slice at the EngineHost root. Frames are transcribed from the
/// same frozen captures the Rpc tests replay (OD-07 — no live model turn, no omp process is spawned):
/// the 19.1.1 handshake capture (<c>rpc-observed-19.1.1-20261123T202249Z.ndjson</c>: <c>ready</c> seq 1,
/// <c>available_commands_update</c> seq 3, <c>negotiate_protocol</c>/<c>get_state</c> responses) and the
/// verified-18.12.6 prompt-path shapes (<c>prompt</c> admission, <c>message_update</c> text delta,
/// <c>prompt_result</c>). <c>ForRpc</c> itself takes an <see cref="OmpRpcClient"/> and would spawn omp on
/// first use, so the connected tests build the host over the exact factory line <c>ForRpc</c> uses
/// (<c>() =&gt; factory.CreateSession(string.Empty)</c>) with the factory's internal starter substituted
/// by a scripted transport — the <see cref="RpcHarnessSessionFactory"/> wrapper and its satellites are
/// the production ones throughout.
/// </summary>
public sealed class InProcessEngineHostRpcTests
{
    private static readonly EngineHostDescriptor Descriptor = new() { Kind = EngineHostKind.InProcess, Isolation = Isolation.Process, Attestation = Attestation.None };

    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864,"supportsBashExcludeFromContext":true}""";

    private const string StateData = """{"model":{"id":"glm-5.3-flash","provider":"zai"},"sessionFile":"/tmp/s.jsonl"}""";

    private const string CommandsLine =
        """{"type":"available_commands_update","commands":[{"name":"security","description":"Plan, run, inspect, import, and compare OMP-native security scans","input":{"hint":"<plan|scan|status|cancel|scans|show|import|export|validate|compare|disposition>"}}]}""";

    /// <summary>
    /// Connect binds the wrapper's satellites through the host (current model from <c>get_state</c>,
    /// advertised commands, live steering), the scripted <c>sessionFile</c> is published as the native
    /// session id, and one prompt streams its text delta to completion with steering supported mid-turn.
    /// </summary>
    [Fact]
    public async Task ConnectBindsSatellitesAndOnePromptStreamsToCompletion()
    {
        var ct = TestContext.Current.CancellationToken;
        string? promptId = null;
        var starter = new ScriptedStarter((command, id) =>
        {
            if (!string.Equals(command, "prompt", StringComparison.Ordinal))
            {
                return null;
            }

            promptId = id;
            return [PromptAdmission(id), TextDelta("ok")];
        });

        var host = CreateScriptedHost(starter);
        try
        {
            int idChanged = 0;
            host.NativeSessionIdChanged += (_, _) => Interlocked.Increment(ref idChanged);

            await host.ConnectAsync(ct).ConfigureAwait(true);

            starter.Starts.ShouldBe(1);
            host.Models.CurrentModelId.ShouldBe("glm-5.3-flash");
            host.SessionCommands.Commands.ShouldBe(
            [
                new OmpCommand(
                    "security",
                    "Plan, run, inspect, import, and compare OMP-native security scans",
                    "<plan|scan|status|cancel|scans|show|import|export|validate|compare|disposition>"),
            ]);
            host.NativeSessionId.ShouldBe("/tmp/s.jsonl");
            Volatile.Read(ref idChanged).ShouldBe(1);

            IAsyncEnumerator<HarnessEvent> enumerator = host.SubmitPromptAsync("reply ok", new PromptOptions(), ct).GetAsyncEnumerator(ct);
            try
            {
                (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
                enumerator.Current.ShouldBe(new TextDeltaEvent("ok"));

                host.LiveSteering.IsSupported.ShouldBeTrue();

                starter.LastTransport!.Emit(PromptResult(promptId!, "completed"));
                (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
                enumerator.Current.ShouldBe(new CompletedEvent("completed"));
                (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeFalse();
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Fork is a named RPC protocol gap (OQ-523): the RPC session offers no fork, so the host refuses with <see cref="NotSupportedException"/>.</summary>
    [Fact]
    public async Task ForkIsNotSupportedOnRpc()
    {
        var ct = TestContext.Current.CancellationToken;
        var starter = new ScriptedStarter((_, _) => null);
        var host = CreateScriptedHost(starter);
        try
        {
            _ = await Should.ThrowAsync<NotSupportedException>(() => host.ForkAsync(ct)).ConfigureAwait(true);
            starter.Starts.ShouldBe(1);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Disposing an unconnected host spawns nothing: the scripted factory is never asked to start, and a
    /// real <c>ForRpc</c> host pointed at a missing omp binary disposes cleanly because the binary is only
    /// probed on the first session start.
    /// </summary>
    [Fact]
    public async Task DisposingUnconnectedForRpcHostSpawnsNothing()
    {
        var starter = new ScriptedStarter((_, _) => null);
        var scripted = CreateScriptedHost(starter);
        await scripted.DisposeAsync().ConfigureAwait(true);
        starter.Starts.ShouldBe(0);

        _ = Should.Throw<ArgumentNullException>(() => InProcessEngineHost.ForRpc(null!));

        var client = new OmpRpcClient(new OmpRpcClientOptions { Cwd = Path.GetTempPath(), OmpPath = "/nonexistent/omp-for-rpc-host-test" });
        var host = InProcessEngineHost.ForRpc(client);
        host.Descriptor.Kind.ShouldBe(EngineHostKind.InProcess);
        await host.DisposeAsync().ConfigureAwait(true);
    }

    private static InProcessEngineHost CreateScriptedHost(ScriptedStarter starter)
    {
        var factory = new RpcHarnessSessionFactory(starter);
        return new InProcessEngineHost(() => factory.CreateSession(string.Empty), Descriptor);
    }

    private static string Ok(string id, string command, string dataJson) =>
        "{\"id\":\"" + id + "\",\"type\":\"response\",\"command\":\"" + command + "\",\"success\":true,\"data\":" + dataJson + "}";

    private static string PromptAdmission(string id) =>
        "{\"id\":\"" + id + "\",\"type\":\"response\",\"command\":\"prompt\",\"success\":true}";

    private static string PromptResult(string id, string status) =>
        "{\"type\":\"prompt_result\",\"id\":\"" + id + "\",\"agentInvoked\":true,\"status\":\"" + status + "\",\"sessionSettled\":true}";

    private static string TextDelta(string delta) =>
        "{\"type\":\"message_update\",\"messageId\":\"m1\",\"message\":{},\"assistantMessageEvent\":{\"type\":\"text_delta\",\"contentIndex\":0,\"delta\":\""
        + delta
        + "\"}}";

    private static string IdOf(string commandLine) => JsonNode.Parse(commandLine)!["id"]!.GetValue<string>();

    private static string TypeOf(string commandLine) => JsonNode.Parse(commandLine)!["type"]!.GetValue<string>();

    private static IEnumerable<string>? Handshake(string command, string id) => command switch
    {
        "negotiate_protocol" => [Ok(id, command, """{"protocolVersion":2}""")],
        "get_state" => [Ok(id, command, StateData)],
        "switch_session" => [Ok(id, command, """{"cancelled":false}""")],
        _ => null,
    };

    /// <summary>Scripted <see cref="IRpcTransport"/>: stdout lines are queued for the session pump; every written command is recorded and answered synchronously.</summary>
    private sealed class ScriptedTransport : IRpcTransport
    {
        private readonly Channel<string> _stdout = Channel.CreateUnbounded<string>();
        private readonly Func<string, IEnumerable<string>>? _respond;
        private readonly List<string> _sent = [];
        private readonly System.Threading.Lock _gate = new();

        public ScriptedTransport(Func<string, IEnumerable<string>>? respond = null)
        {
            _respond = respond;
        }

        /// <summary>Gets the command lines written so far, oldest first.</summary>
        public IReadOnlyList<string> Sent
        {
            get
            {
                lock (_gate)
                {
                    return _sent.ToArray();
                }
            }
        }

        /// <summary>Delivers one stdout line to the session.</summary>
        /// <param name="line">The frame line.</param>
        public void Emit(string line) => _stdout.Writer.TryWrite(line);

        /// <inheritdoc />
        public IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken) =>
            _stdout.Reader.ReadAllAsync(cancellationToken);

        /// <inheritdoc />
        public Task SendLineAsync(string line, CancellationToken cancellationToken)
        {
            IEnumerable<string> replies;
            lock (_gate)
            {
                _sent.Add(line);
                replies = _respond?.Invoke(line) ?? [];
            }

            foreach (string reply in replies)
            {
                Emit(reply);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task CloseInputAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(0);

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _stdout.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Opens each engine session over a fresh scripted transport, counting starts. Stands in for the process boundary, as in the Rpc tests' starter.</summary>
    private sealed class ScriptedStarter : IRpcSessionStarter
    {
        private readonly Func<string, string, IEnumerable<string>?> _scripted;
        private int _starts;

        public ScriptedStarter(Func<string, string, IEnumerable<string>?> scripted)
        {
            _scripted = scripted;
        }

        /// <summary>Gets how many engine sessions have been started.</summary>
        public int Starts => Volatile.Read(ref _starts);

        /// <summary>Gets the transport of the most recent start.</summary>
        public ScriptedTransport? LastTransport { get; private set; }

        /// <inheritdoc />
        public Task<IOmpSession> CreateAsync(CancellationToken cancellationToken) => OpenAsync(null, cancellationToken);

        /// <inheritdoc />
        public Task<IOmpSession> ResumeAsync(string sessionPath, CancellationToken cancellationToken) => OpenAsync(sessionPath, cancellationToken);

        private async Task<IOmpSession> OpenAsync(string? sessionPath, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _starts);
            var transport = new ScriptedTransport(line =>
            {
                string command = TypeOf(line);
                string id = IdOf(line);
                return Handshake(command, id) ?? _scripted(command, id) ?? Array.Empty<string>();
            });
            transport.Emit(ReadyLine);
            transport.Emit(CommandsLine);
            LastTransport = transport;

            var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            if (sessionPath is null)
            {
                await session.CreateAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await session.ResumeAsync(sessionPath, cancellationToken).ConfigureAwait(false);
            }

            return session;
        }
    }
}
