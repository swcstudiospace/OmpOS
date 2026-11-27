using System.Text.Json.Nodes;
using System.Threading.Channels;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Rpc;

namespace Autonogrammer.Harness.ContractTests;

/// <summary>
/// An <see cref="IRpcTransport"/> whose peer is a scripted omp RPC engine (Phase 80 Task 12, replay only —
/// OD-07: no omp process is spawned and no live model turn runs). Frame shapes are transcribed from the
/// frozen captures the RPC tests cite: the 19.1.1 handshake (<c>rpc-observed-19.1.1-20261123T202249Z.ndjson</c>,
/// ledger R-READY/R-NEGOTIATE/R-CAPS) and the verified-18.12.6 prompt path
/// (<c>rpc-live-20261021T171351Z.ndjson</c>, ledger <c>prompt</c>/<c>abort</c> rows), in the same reduced
/// form <c>Autonogrammer.Harness.Rpc.Tests</c> already scripts (<c>RpcTestData</c>). No frame kind or
/// command is invented here: the engine answers only <c>negotiate_protocol</c>, <c>get_state</c>,
/// <c>switch_session</c>, <c>prompt</c> (plain and <c>streamingBehavior</c> lanes) and <c>abort</c>.
/// A plain prompt is held open after its first text delta until <see cref="CompletePrompt"/> is called,
/// so steering and cancellation land mid-turn exactly as the ACP script holds its prompt.
/// </summary>
internal sealed class ScriptedRpcTransport : IRpcTransport
{
    /// <summary>The model both scripted peers report, so the parity outcome compares equal: the ACP capture's current model.</summary>
    internal const string ModelId = "zai/glm-5.3-flash";

    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}""";

    private readonly Channel<string> _stdout = Channel.CreateUnbounded<string>();
    private readonly List<string> _sent = [];
    private readonly System.Threading.Lock _gate = new();
    private readonly string _sessionFile;
    private readonly string _firstDelta;
    private readonly Channel<bool> _promptSeen = Channel.CreateBounded<bool>(1);
    private string? _promptId;

    /// <summary>Initializes a new instance of the <see cref="ScriptedRpcTransport"/> class and queues the <c>ready</c> frame.</summary>
    /// <param name="sessionFile">The <c>get_state.sessionFile</c> resume key this engine reports. Distinct per engine instance.</param>
    /// <param name="firstDelta">The text of the first streamed delta a prompt produces. Distinct per instance in the isolation scenario.</param>
    public ScriptedRpcTransport(string sessionFile, string firstDelta = "Hel")
    {
        _sessionFile = sessionFile;
        _firstDelta = firstDelta;
        Emit(ReadyLine);
    }

    /// <summary>Gets the command lines the session wrote, oldest first.</summary>
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

    /// <summary>Injects one <c>available_commands_update</c> frame carrying the supplied <c>commands</c> JSON array, in the frozen 19.1.1 capture shape (ledger R-UNSOLICITED).</summary>
    /// <param name="commandsJson">A JSON array of command objects with <c>name</c>, optional <c>description</c> and optional <c>input.hint</c>.</param>
    public void DeliverAvailableCommands(string commandsJson) => Emit($$"""{"type":"available_commands_update","commands":{{commandsJson}}}""");

    /// <summary>Waits until the engine has seen a plain prompt command.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public async Task WaitForPromptAsync(CancellationToken cancellationToken) =>
        _ = await _promptSeen.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Ends the held prompt: the remaining streamed deltas, then the terminal <c>prompt_result</c>.</summary>
    public void CompletePrompt()
    {
        string promptId = _promptId ?? throw new InvalidOperationException("No prompt is held.");
        Emit(ThinkingDelta("hmm"));
        Emit("""{"type":"tool_execution_start","toolCallId":"t1","toolName":"read"}""");
        Emit("""{"type":"tool_execution_end","toolCallId":"t1","isError":false}""");
        Emit(TextDelta("lo"));
        Emit(PromptResult(promptId, "completed"));
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken) =>
        _stdout.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public Task SendLineAsync(string line, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _sent.Add(line);
        }

        foreach (string reply in Respond(line))
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
        _ = _stdout.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private void Emit(string line) => _ = _stdout.Writer.TryWrite(line);

    private IEnumerable<string> Respond(string line)
    {
        JsonNode node = JsonNode.Parse(line)!;
        string command = node["type"]!.GetValue<string>();
        string id = node["id"]!.GetValue<string>();
        switch (command)
        {
            case "negotiate_protocol":
                return [Ok(id, command, """{"protocolVersion":2}""")];
            case "get_state":
                return [Ok(id, command, StateData())];
            case "switch_session":
                return [Ok(id, command, """{"cancelled":false}""")];
            case "prompt" when node["streamingBehavior"] is not null:
                return [Ok(id, command, """{"agentInvoked":true}""")];
            case "prompt":
                _promptId = id;
                _ = _promptSeen.Writer.TryWrite(true);
                // The live capture's admission (seq 5) is a bare success response with no data member.
                return ["{\"id\":\"" + id + "\",\"type\":\"response\",\"command\":\"prompt\",\"success\":true}", TextDelta(_firstDelta)];
            case "abort":
                return _promptId is { } promptId
                    ? [Ok(id, command, "null"), PromptResult(promptId, "aborted")]
                    : [Ok(id, command, "null")];
            default:
                return [];
        }
    }

    private string StateData() =>
        "{\"model\":{\"id\":\"" + ModelId + "\",\"provider\":\"zai\"},\"sessionFile\":\"" + _sessionFile + "\"}";

    private static string Ok(string id, string command, string dataJson) =>
        "{\"id\":\"" + id + "\",\"type\":\"response\",\"command\":\"" + command + "\",\"success\":true,\"data\":" + dataJson + "}";

    private static string PromptResult(string id, string status) =>
        "{\"type\":\"prompt_result\",\"id\":\"" + id + "\",\"agentInvoked\":true,\"status\":\"" + status + "\",\"sessionSettled\":true}";

    private static string TextDelta(string delta) =>
        "{\"type\":\"message_update\",\"messageId\":\"m1\",\"message\":{},\"assistantMessageEvent\":{\"type\":\"text_delta\",\"contentIndex\":0,\"delta\":\""
        + delta
        + "\"}}";

    private static string ThinkingDelta(string delta) =>
        "{\"type\":\"message_update\",\"messageId\":\"m1\",\"message\":{},\"assistantMessageEvent\":{\"type\":\"thinking_delta\",\"contentIndex\":0,\"delta\":\""
        + delta
        + "\"}}";

    /// <summary>
    /// Opens each factory session over a fresh scripted engine and completes its handshake, so the suite
    /// exercises the production-identical wrapper surface (<c>RpcHarnessSession</c> over <c>OmpRpcSession</c>)
    /// through <see cref="RpcHarnessSessionFactory"/> without spawning omp. Every start gets its own
    /// transport: one engine per session, no shared mutable client state (the design-multi property).
    /// </summary>
    internal sealed class Starter : IRpcSessionStarter
    {
        private readonly Func<ScriptedRpcTransport> _transportFactory;
        private readonly List<ScriptedRpcTransport> _transports = [];

        /// <summary>Initializes a new instance of the <see cref="Starter"/> class.</summary>
        /// <param name="transportFactory">Creates the scripted engine for one start.</param>
        public Starter(Func<ScriptedRpcTransport> transportFactory)
        {
            _transportFactory = transportFactory;
        }

        /// <summary>Gets every transport this starter opened, in start order.</summary>
        public IReadOnlyList<ScriptedRpcTransport> Transports => _transports;

        /// <inheritdoc />
        public Task<IOmpSession> CreateAsync(CancellationToken cancellationToken) => OpenAsync(null, cancellationToken);

        /// <inheritdoc />
        public Task<IOmpSession> ResumeAsync(string sessionPath, CancellationToken cancellationToken) =>
            OpenAsync(sessionPath, cancellationToken);

        private async Task<IOmpSession> OpenAsync(string? sessionPath, CancellationToken cancellationToken)
        {
            ScriptedRpcTransport transport = _transportFactory();
            _transports.Add(transport);
            var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            try
            {
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
            catch
            {
                await session.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
