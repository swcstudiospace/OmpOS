using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Native.Tests.Send;

namespace Autonogrammer.Native.Tests.SlashPalette;

/// <summary>
/// A scripted <c>omp acp</c> peer that speaks the slash-command protocol omp 18.12.x speaks: <c>session/new</c> is answered with a session and,
/// right behind the response, an <c>available_commands_update</c> that lists the commands (built-ins with input hints and a skill). A prompt is
/// answered with one text chunk. <see cref="Advertise"/> pushes a new list as omp does after a plugin reload. Every frame the client sent is
/// recorded, so a test can prove that listing commands sent nothing but the handshake.
/// </summary>
internal sealed class CommandsOmp
{
    private readonly System.Threading.Lock _gate = new();
    private readonly List<JsonObject> _frames = [];
    private readonly List<ScriptedAgentTransport> _transports = [];
    private string _sessionId = string.Empty;
    private int _sessions;

    /// <summary>Gets the transport factory to give the head.</summary>
    public Func<AcpClientOptions, IAcpTransport> TransportFactory => Create;

    /// <summary>Gets how many omp children were started.</summary>
    public int Launches
    {
        get
        {
            lock (_gate)
            {
                return _transports.Count;
            }
        }
    }

    /// <summary>Gets the frames the client sent for one method, in order.</summary>
    /// <param name="method">The JSON-RPC method.</param>
    /// <returns>The frames.</returns>
    public IReadOnlyList<JsonObject> Requests(string method)
    {
        lock (_gate)
        {
            return [.. _frames.Where(frame => string.Equals(frame["method"]?.GetValue<string>(), method, StringComparison.Ordinal))];
        }
    }

    /// <summary>Pushes an <c>available_commands_update</c> to the newest child, as omp does after a plugin reload.</summary>
    /// <param name="commands">The commands to advertise.</param>
    public void Advertise(params OmpCommand[] commands)
    {
        ScriptedAgentTransport transport;
        lock (_gate)
        {
            transport = _transports[^1];
        }

        transport.Push([Encode(CommandsUpdate(commands))]);
    }

    private static byte[] Encode(JsonObject frame) => Encoding.UTF8.GetBytes(frame.ToJsonString());

    private static JsonObject Result(long id, JsonObject result) => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };

    private static JsonObject Initialize() => new()
    {
        ["protocolVersion"] = 1,
        ["agentCapabilities"] = new JsonObject
        {
            ["loadSession"] = true,
            ["sessionCapabilities"] = new JsonObject { ["list"] = new JsonObject(), ["resume"] = new JsonObject(), ["close"] = new JsonObject() },
        },
    };

    private JsonObject CommandsUpdate(OmpCommand[] commands)
    {
        var list = new JsonArray();
        foreach (var command in commands)
        {
            var entry = new JsonObject { ["name"] = command.Name };
            if (command.Description is not null)
            {
                entry["description"] = command.Description;
            }

            if (command.InputHint is not null)
            {
                entry["input"] = new JsonObject { ["hint"] = command.InputHint };
            }

            list.Add(entry);
        }

        return Notify("available_commands_update", new JsonObject { ["availableCommands"] = list });
    }

    private JsonObject Notify(string kind, JsonObject extra)
    {
        extra["sessionUpdate"] = kind;
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "session/update",
            ["params"] = new JsonObject { ["sessionId"] = _sessionId, ["update"] = extra },
        };
    }

    private ScriptedAgentTransport Create(AcpClientOptions launch)
    {
        _ = launch;
        var transport = new ScriptedAgentTransport(Respond);
        lock (_gate)
        {
            _transports.Add(transport);
        }

        return transport;
    }

    private IEnumerable<byte[]> Respond(byte[] frame)
    {
        var node = JsonNode.Parse(frame)!.AsObject();
        lock (_gate)
        {
            _frames.Add(node.DeepClone().AsObject());
        }

        var method = node["method"]?.GetValue<string>();
        if (method is null)
        {
            return [];
        }

        var id = node["id"]?.GetValue<long>() ?? 0;
        switch (method)
        {
            case "initialize":
                return [Encode(Result(id, Initialize()))];
            case "session/new":
                lock (_gate)
                {
                    _sessionId = "native-" + (++_sessions).ToString(CultureInfo.InvariantCulture);
                }

                // omp sends the first available_commands_update right behind the session/new response.
                return
                [
                    Encode(Result(id, new JsonObject { ["sessionId"] = _sessionId })),
                    Encode(CommandsUpdate(
                    [
                        new OmpCommand("security", "Plan, run, inspect, import, and compare OMP-native security scans", "<plan|scan|status>"),
                        new OmpCommand("model", "Show current model selection", null),
                        new OmpCommand("switch", "Switch model for this session only", "[model]"),
                        new OmpCommand("fast", "Toggle fast mode", "[on|ultra|off|status]"),
                        new OmpCommand("skill:review", "Review the change", "arguments"),
                    ])),
                ];
            case "session/prompt":
                return
                [
                    Encode(Notify("agent_message_chunk", new JsonObject { ["content"] = new JsonObject { ["type"] = "text", ["text"] = "ok" } })),
                    Encode(Result(id, new JsonObject { ["stopReason"] = "end_turn" })),
                ];
            default:
                return node["id"] is null ? [] : [Encode(Result(id, new JsonObject()))];
        }
    }
}
