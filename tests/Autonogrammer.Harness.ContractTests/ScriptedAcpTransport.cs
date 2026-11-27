using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.ContractTests;

/// <summary>
/// An <see cref="IAcpTransport"/> whose peer is a scripted omp ACP engine (Phase 80 Task 12, replay only —
/// OD-07: no omp process is spawned and no live model turn runs). The handshake is answered from the frozen
/// capture <c>acp-out.ndjson</c> (linked from <c>Autonogrammer.Harness.Acp.Tests</c>, never copied): the
/// <c>initialize</c> result (id 1, with the observed-19.1.1 additive <c>_meta.steering.supported</c> injected
/// exactly as <c>ScriptedOmpPeer</c> does in the ACP tests) and the <c>session/new</c> result (id 2, whose
/// captured <c>configOptions</c> — mode plus model — back the stateful option script). Prompt, steering,
/// cancel, fork and set-option replies use the same reduced frame shapes the ACP test suite scripts
/// (<c>session/update</c> chunks, <c>_session/steering</c> outcomes, <c>$/cancel_request</c>,
/// <c>session/fork</c>, <c>session/set_config_option</c>). A prompt is held open after its first text chunk
/// until <see cref="CompletePrompt"/> is called, mirroring the RPC script so both peers hold a turn the
/// same way. No <c>authenticate</c> response exists in the capture; it is accepted with an empty result,
/// as in <c>AcpFixtures.Reply</c> — synthesised, not observed omp output.
/// </summary>
internal sealed class ScriptedAcpTransport : IAcpTransport
{
    /// <summary>The session identifier recorded in <c>acp-out.ndjson</c>.</summary>
    internal const string CapturedSessionId = "01a1582d-a7c8-724b-a542-695b31c7d7b4";

    /// <summary>The identifier the scripted <c>session/fork</c> answer returns.</summary>
    internal const string ForkedSessionId = "forked-native-id";

    private static readonly (string Initialize, string SessionNew) Captured = LoadCaptured();

    private readonly Channel<byte[]> _inbound = Channel.CreateUnbounded<byte[]>();
    private readonly List<byte[]> _sent = [];
    private readonly System.Threading.Lock _gate = new();
    private readonly Dictionary<string, string> _currentValues = new(StringComparer.Ordinal);
    private readonly Channel<bool> _promptSeen = Channel.CreateBounded<bool>(1);
    private long _promptId;
    private bool _closed;

    /// <summary>Gets a snapshot of every frame the session has sent, in order.</summary>
    public IReadOnlyList<byte[]> Sent
    {
        get
        {
            lock (_gate)
            {
                return _sent.ToArray();
            }
        }
    }

    /// <summary>Injects one <c>available_commands_update</c> notification carrying the supplied <c>availableCommands</c> JSON array, in the <c>session/update</c> shape the ACP suite scripts.</summary>
    /// <param name="availableCommandsJson">A JSON array of command objects with <c>name</c>, optional <c>description</c> and optional <c>input.hint</c>.</param>
    public void DeliverAvailableCommands(string availableCommandsJson) => Deliver(Utf8("{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{\"sessionId\":\"" + CapturedSessionId + "\",\"update\":{\"sessionUpdate\":\"available_commands_update\",\"availableCommands\":" + availableCommandsJson + "}}}"));

    /// <summary>Waits until the engine has seen a <c>session/prompt</c> request.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public async Task WaitForPromptAsync(CancellationToken cancellationToken) =>
        _ = await _promptSeen.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Ends the held prompt: the remaining <c>session/update</c> chunks, then the prompt result.</summary>
    public void CompletePrompt()
    {
        Deliver(Update("""{"sessionUpdate":"agent_thought_chunk","content":{"type":"text","text":"hmm"}}"""));
        Deliver(Update("""{"sessionUpdate":"tool_call","toolCallId":"t1","title":"read","kind":"read","status":"pending"}"""));
        Deliver(Update("""{"sessionUpdate":"tool_call_update","toolCallId":"t1","status":"completed"}"""));
        Deliver(Update("""{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"lo"}}"""));
        Deliver(Result(_promptId, """{"stopReason":"end_turn"}"""));
    }

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
        byte[] copy = frame.ToArray();
        lock (_gate)
        {
            if (_closed)
            {
                throw new DisconnectedHarnessException("The scripted ACP transport is closed.", "scripted");
            }

            _sent.Add(copy);
        }

        foreach (byte[] reply in Respond(copy))
        {
            Deliver(reply);
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
                throw new DisconnectedHarnessException("The scripted ACP transport closed.", "scripted");
            }

            while (_inbound.Reader.TryRead(out byte[]? frame))
            {
                yield return frame;
            }
        }
    }

    /// <inheritdoc />
    public Task CloseAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _closed = true;
        }

        _ = _inbound.Writer.TryComplete();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await CloseAsync(CancellationToken.None).ConfigureAwait(false);

    private void Deliver(byte[] frame) => _ = _inbound.Writer.TryWrite(frame);

    private IEnumerable<byte[]> Respond(byte[] frame)
    {
        JsonObject node = JsonNode.Parse(frame)!.AsObject();
        string? method = node["method"]?.GetValue<string>();
        if (method is null)
        {
            // A response from the session (for example a permission answer): the script sends none back.
            return [];
        }

        if (string.Equals(method, "$/cancel_request", StringComparison.Ordinal))
        {
            return [Result(_promptId, """{"stopReason":"cancelled"}""")];
        }

        if (node["id"] is not { } idNode)
        {
            // A notification (session/cancel): the cancel itself is answered through $/cancel_request.
            return [];
        }

        long id = idNode.GetValue<long>();
        switch (method)
        {
            case "initialize":
                return [Utf8(WithSteeringMeta(Captured.Initialize, id))];
            case "session/new":
                return [Result(id, "{\"sessionId\":\"" + CapturedSessionId + "\",\"configOptions\":" + CurrentOptionsJson() + "}")];
            case "session/load":
                return [Result(id, "{\"configOptions\":" + CurrentOptionsJson() + "}")];
            case "session/prompt":
                _promptId = id;
                _ = _promptSeen.Writer.TryWrite(true);
                return [Update("""{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"Hel"}}""")];
            case "_session/steering":
                string queue = node["params"]?["_meta"]?["steering"]?["queue"]?.GetValue<string>() ?? "steering";
                return [Result(id, "{\"outcome\":\"injected\",\"queue\":\"" + queue + "\"}")];
            case "session/set_config_option":
                string configId = node["params"]!["configId"]!.GetValue<string>();
                _currentValues[configId] = node["params"]!["value"]!.GetValue<string>();
                return [Result(id, "{\"configOptions\":" + CurrentOptionsJson() + "}")];
            case "session/fork":
                return [Result(id, "{\"sessionId\":\"" + ForkedSessionId + "\"}")];
            default:
                // authenticate, session/close and anything else the handshake sends: an empty result.
                return [Result(id, "{}")];
        }
    }

    /// <summary>The captured option list with every scripted set applied to its <c>currentValue</c>.</summary>
    private string CurrentOptionsJson()
    {
        JsonArray options = JsonNode.Parse(Captured.SessionNew)!["result"]!["configOptions"]!.AsArray().DeepClone().AsArray();
        foreach (JsonNode? option in options)
        {
            if (option is JsonObject entry
                && entry["id"]?.GetValue<string>() is { } optionId
                && _currentValues.TryGetValue(optionId, out string? value))
            {
                entry["currentValue"] = value;
            }
        }

        return options.ToJsonString();
    }

    private static byte[] Update(string update) =>
        Utf8("{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{\"sessionId\":\"" + CapturedSessionId + "\",\"update\":" + update + "}}");

    private static byte[] Result(long id, string result) =>
        Utf8(string.Create(CultureInfo.InvariantCulture, $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{result}}}"));

    private static string WithSteeringMeta(string initialize, long id)
    {
        JsonObject node = JsonNode.Parse(initialize)!.AsObject();
        node["id"] = id;
        node["result"]!["_meta"] = new JsonObject { ["steering"] = new JsonObject { ["supported"] = true } };
        return node.ToJsonString();
    }

    private static (string Initialize, string SessionNew) LoadCaptured()
    {
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TestData", "frames", "acp-out.ndjson"));
        string? initialize = null;
        string? sessionNew = null;
        foreach (string line in lines)
        {
            if (line.Length == 0)
            {
                continue;
            }

            long? id = JsonNode.Parse(line)!["id"]?.GetValue<long>();
            if (id == 1)
            {
                initialize = line;
            }
            else if (id == 2)
            {
                sessionNew = line;
            }
        }

        return (initialize ?? throw new InvalidOperationException("The ACP capture has no initialize response (id 1)."),
            sessionNew ?? throw new InvalidOperationException("The ACP capture has no session/new response (id 2)."));
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
