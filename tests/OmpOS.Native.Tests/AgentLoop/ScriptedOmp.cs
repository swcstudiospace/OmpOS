using System.Text;
using System.Text.Json.Nodes;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Native.Tests.Send;

namespace OmpOS.Native.Tests.AgentLoop;

/// <summary>
/// A scripted fleet of <c>omp acp</c> peers. Every engine the head composes asks <see cref="TransportFactory"/> for a transport, and
/// each transport talks to its own peer, so N sessions mean N peers. A peer opens <c>native-1</c>, <c>native-2</c>, and so on for
/// <c>session/new</c>, accepts <c>session/load</c> for any identifier, answers a prompt with one text chunk and a completion, and can hold
/// a prompt open after its first chunk. The fleet records every launch (the options the transport was built from) and every frame the
/// client sent, and counts the transports that were closed, so a test can assert that nothing is left running.
/// </summary>
internal sealed class ScriptedOmp
{
    private readonly System.Threading.Lock _gate = new();
    private readonly List<AcpClientOptions> _launches = [];
    private readonly List<JsonObject> _frames = [];
    private int _sessions;
    private int _closedTransports;

    /// <summary>Gets the text each prompt is answered with.</summary>
    public string Reply { get; init; } = "Hi from the scripted omp";

    /// <summary>Gets a value indicating whether a prompt streams its first chunk and then stays open until the client cancels it.</summary>
    public bool HoldAfterFirstChunk { get; init; }

    /// <summary>Gets a value indicating whether <c>initialize</c> advertises <c>authMethods: [{id:"agent"}]</c>, as omp 18.12.x does in every environment.</summary>
    public bool AdvertiseAuthentication { get; init; }

    /// <summary>Gets a value indicating whether <c>initialize</c> advertises <c>_meta.steering.supported</c>, as omp 18.12.x does.</summary>
    public bool SteeringSupported { get; init; }

    /// <summary>Gets the <c>outcome</c> every <c>_session/steering</c> request is answered with: <c>injected</c>, <c>promptRequired</c> or <c>failed</c>.</summary>
    public string SteerOutcome { get; init; } = "injected";

    /// <summary>Gets the <c>reason</c> of a <c>failed</c> steering answer, or null for none.</summary>
    public string? SteerReason { get; init; }

    /// <summary>Gets the JSON-RPC error code every <c>_session/steering</c> request is answered with instead of a result, or null to answer with a result.</summary>
    public int? SteerErrorCode { get; init; }

    /// <summary>Gets the transport factory to give the head.</summary>
    public Func<AcpClientOptions, IAcpTransport> TransportFactory => Create;

    /// <summary>Gets the options of every transport created, in order.</summary>
    public IReadOnlyList<AcpClientOptions> Launches
    {
        get
        {
            lock (_gate)
            {
                return [.. _launches];
            }
        }
    }

    /// <summary>Gets a copy of every frame the client sent to any peer, in order.</summary>
    public IReadOnlyList<JsonObject> Frames
    {
        get
        {
            lock (_gate)
            {
                return [.. _frames];
            }
        }
    }

    /// <summary>Gets how many transports were closed.</summary>
    public int ClosedTransports => Volatile.Read(ref _closedTransports);

    /// <summary>Gets the frames the client sent for one method.</summary>
    /// <param name="method">The JSON-RPC method.</param>
    /// <returns>The frames, in order.</returns>
    public IReadOnlyList<JsonObject> Requests(string method) =>
        [.. Frames.Where(frame => string.Equals(frame["method"]?.GetValue<string>(), method, StringComparison.Ordinal))];

    private static JsonObject ResultFrame(long id, JsonObject result) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["result"] = result,
    };

    private static byte[] Encode(JsonObject frame) => Encoding.UTF8.GetBytes(frame.ToJsonString());

    private JsonObject Initialize()
    {
        var result = new JsonObject
        {
            ["protocolVersion"] = 1,
            ["agentCapabilities"] = new JsonObject
            {
                ["loadSession"] = true,
                ["mcpCapabilities"] = new JsonObject { ["http"] = true, ["sse"] = true },
                ["promptCapabilities"] = new JsonObject { ["embeddedContext"] = true, ["image"] = true },
                ["sessionCapabilities"] = new JsonObject
                {
                    ["list"] = new JsonObject(),
                    ["fork"] = new JsonObject(),
                    ["resume"] = new JsonObject(),
                    ["close"] = new JsonObject(),
                },
            },
        };
        if (AdvertiseAuthentication)
        {
            result["authMethods"] = new JsonArray(new JsonObject { ["id"] = "agent", ["name"] = "Use existing local credentials" });
        }

        if (SteeringSupported)
        {
            result["_meta"] = new JsonObject { ["steering"] = new JsonObject { ["supported"] = true } };
        }

        return result;
    }

    private TrackingTransport Create(AcpClientOptions options)
    {
        lock (_gate)
        {
            _launches.Add(options);
        }

        var peer = new Peer(this);
        return new TrackingTransport(peer.Respond, this);
    }

    private void Record(JsonObject frame)
    {
        lock (_gate)
        {
            _frames.Add(frame);
        }
    }

    /// <summary>One <c>omp acp</c> child: it knows the session it opened or loaded, and the prompt it holds.</summary>
    private sealed class Peer(ScriptedOmp owner)
    {
        private string _sessionId = string.Empty;
        private long _pendingPrompt;

        public IEnumerable<byte[]> Respond(byte[] frame)
        {
            var node = JsonNode.Parse(frame)!.AsObject();
            owner.Record(node.DeepClone().AsObject());
            var method = node["method"]?.GetValue<string>();
            if (method is null)
            {
                return [];
            }

            var id = node["id"]?.GetValue<long>() ?? 0;
            switch (method)
            {
                case "initialize":
                    return [Encode(ResultFrame(id, owner.Initialize()))];
                case "session/new":
                    _sessionId = "native-" + Interlocked.Increment(ref owner._sessions).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    return [Encode(ResultFrame(id, new JsonObject { ["sessionId"] = _sessionId }))];
                case "session/load":
                    _sessionId = node["params"]!["sessionId"]!.GetValue<string>();
                    return [Encode(ResultFrame(id, new JsonObject()))];
                case "session/prompt":
                    _pendingPrompt = id;
                    return owner.HoldAfterFirstChunk
                        ? [Encode(Update(_sessionId, owner.Reply))]
                        : [Encode(Update(_sessionId, owner.Reply)), Encode(EndPrompt("end_turn"))];
                case "_session/steering":
                    return [Encode(owner.SteerAnswer(id, node["params"]!.AsObject()))];
                case "session/cancel":
                    return _pendingPrompt == 0 ? [] : [Encode(EndPrompt("cancelled"))];
                default:
                    return node["id"] is null ? [] : [Encode(ResultFrame(id, new JsonObject()))];
            }
        }

        private static JsonObject Update(string sessionId, string text) => new()
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "session/update",
            ["params"] = new JsonObject
            {
                ["sessionId"] = sessionId,
                ["update"] = new JsonObject
                {
                    ["sessionUpdate"] = "agent_message_chunk",
                    ["content"] = new JsonObject { ["type"] = "text", ["text"] = text },
                },
            },
        };

        private JsonObject EndPrompt(string stopReason)
        {
            var reply = ResultFrame(_pendingPrompt, new JsonObject { ["stopReason"] = stopReason });
            _pendingPrompt = 0;
            return reply;
        }
    }

    private JsonObject SteerAnswer(long id, JsonObject parameters)
    {
        if (SteerErrorCode is { } code)
        {
            return new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new JsonObject { ["code"] = code, ["message"] = "Invalid params" },
            };
        }

        var followUp = parameters["_meta"]?["steering"]?["queue"]?.GetValue<string>() is "followUp";
        var result = new JsonObject { ["outcome"] = SteerOutcome, ["queue"] = followUp ? "followUp" : "steering" };
        if (SteerReason is not null)
        {
            result["reason"] = SteerReason;
        }

        return ResultFrame(id, result);
    }

    /// <summary>A transport that counts the first time it is closed or disposed.</summary>
    private sealed class TrackingTransport(Func<byte[], IEnumerable<byte[]>> responder, ScriptedOmp owner) : IAcpTransport
    {
        private readonly ScriptedAgentTransport _inner = new(responder);
        private int _closed;

        public Task ConnectAsync(CancellationToken cancellationToken) => _inner.ConnectAsync(cancellationToken);

        public Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken) => _inner.SendAsync(frame, cancellationToken);

        public IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) => _inner.ReceiveAsync(cancellationToken);

        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            Count();
            await _inner.CloseAsync(cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            Count();
            await _inner.DisposeAsync().ConfigureAwait(false);
        }

        private void Count()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 0)
            {
                Interlocked.Increment(ref owner._closedTransports);
            }
        }
    }
}
