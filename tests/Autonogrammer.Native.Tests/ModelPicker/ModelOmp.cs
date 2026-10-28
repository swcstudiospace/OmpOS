using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Native.Tests.Send;

namespace Autonogrammer.Native.Tests.ModelPicker;

/// <summary>
/// A scripted <c>omp acp</c> peer that speaks the model protocol omp 18.12.x speaks: <c>session/new</c> and <c>session/load</c> answer with
/// <c>configOptions</c> (a mode option and a model option), <c>session/set_config_option</c> switches the model and answers with the rebuilt
/// options (an unknown value is a JSON-RPC error), and a prompt is answered with one text chunk. The model belongs to the omp session, not
/// to the child process: it survives a close and a <c>session/load</c>, as an omp session does. Every frame the client sent is recorded.
/// </summary>
internal sealed class ModelOmp
{
    private static readonly (string Value, string Name)[] Models =
    [
        ("anthropic/claude-sonnet", "Claude Sonnet"),
        ("anthropic/claude-opus", "Claude Opus"),
        ("openai/gpt-x", "GPT X"),
    ];

    private readonly System.Threading.Lock _gate = new();
    private readonly List<JsonObject> _frames = [];
    private readonly List<ScriptedAgentTransport> _transports = [];
    private readonly List<AcpClientOptions> _launches = [];
    private string _sessionId = string.Empty;
    private string _current = Models[0].Value;
    private int _sessions;

    /// <summary>Gets the transport factory to give the head.</summary>
    public Func<AcpClientOptions, IAcpTransport> TransportFactory => Create;

    /// <summary>Gets how many omp children were started, one per engine the head composed.</summary>
    public int Launches
    {
        get
        {
            lock (_gate)
            {
                return _launches.Count;
            }
        }
    }

    /// <summary>Gets the model the omp session uses now.</summary>
    public string Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
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

    /// <summary>Changes the model as omp does when a slash command changes it, and pushes the <c>config_option_update</c> to the newest child.</summary>
    /// <param name="model">The model to use.</param>
    public void ChangeModelOnItsOwn(string model)
    {
        ScriptedAgentTransport transport;
        string sessionId;
        lock (_gate)
        {
            _current = model;
            transport = _transports[^1];
            sessionId = _sessionId;
        }

        transport.Push([Encode(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "session/update",
            ["params"] = new JsonObject
            {
                ["sessionId"] = sessionId,
                ["update"] = new JsonObject { ["sessionUpdate"] = "config_option_update", ["configOptions"] = Options() },
            },
        })]);
    }

    private static byte[] Encode(JsonObject frame) => Encoding.UTF8.GetBytes(frame.ToJsonString());

    private static JsonObject Result(long id, JsonObject result) => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };

    private static JsonObject Error(long id, string message) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["error"] = new JsonObject { ["code"] = -32603, ["message"] = message },
    };

    private static JsonObject Initialize() => new()
    {
        ["protocolVersion"] = 1,
        ["agentCapabilities"] = new JsonObject
        {
            ["loadSession"] = true,
            ["sessionCapabilities"] = new JsonObject { ["list"] = new JsonObject(), ["resume"] = new JsonObject(), ["close"] = new JsonObject() },
        },
    };

    private JsonArray Options()
    {
        var model = new JsonObject
        {
            ["id"] = "model",
            ["name"] = "Model",
            ["category"] = "model",
            ["type"] = "select",
            ["currentValue"] = Current,
            ["options"] = new JsonArray([.. Models.Select(static entry => (JsonNode)new JsonObject { ["value"] = entry.Value, ["name"] = entry.Name })]),
        };
        var mode = new JsonObject
        {
            ["id"] = "mode",
            ["name"] = "Mode",
            ["category"] = "mode",
            ["type"] = "select",
            ["currentValue"] = "default",
            ["options"] = new JsonArray(new JsonObject { ["value"] = "default", ["name"] = "Default" }),
        };
        return new JsonArray(mode, model);
    }

    private ScriptedAgentTransport Create(AcpClientOptions launch)
    {
        var transport = new ScriptedAgentTransport(Respond);
        lock (_gate)
        {
            _launches.Add(launch);
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

                return [Encode(Result(id, new JsonObject { ["sessionId"] = _sessionId, ["configOptions"] = Options() }))];
            case "session/load":
                lock (_gate)
                {
                    _sessionId = node["params"]!["sessionId"]!.GetValue<string>();
                }

                return [Encode(Result(id, new JsonObject { ["configOptions"] = Options() }))];
            case "session/set_config_option":
                return SetOption(id, node["params"]!["value"]!.GetValue<string>());
            case "session/prompt":
                return [Encode(Update("hello")), Encode(Result(id, new JsonObject { ["stopReason"] = "end_turn" }))];
            default:
                return node["id"] is null ? [] : [Encode(Result(id, new JsonObject()))];
        }
    }

    private IEnumerable<byte[]> SetOption(long id, string value)
    {
        if (!Models.Any(model => string.Equals(model.Value, value, StringComparison.Ordinal)))
        {
            return [Encode(Error(id, "Unknown ACP model: " + value))];
        }

        lock (_gate)
        {
            _current = value;
        }

        return [Encode(Result(id, new JsonObject { ["configOptions"] = Options() }))];
    }

    private JsonObject Update(string text) => new()
    {
        ["jsonrpc"] = "2.0",
        ["method"] = "session/update",
        ["params"] = new JsonObject
        {
            ["sessionId"] = _sessionId,
            ["update"] = new JsonObject
            {
                ["sessionUpdate"] = "agent_message_chunk",
                ["content"] = new JsonObject { ["type"] = "text", ["text"] = text },
            },
        },
    };
}
