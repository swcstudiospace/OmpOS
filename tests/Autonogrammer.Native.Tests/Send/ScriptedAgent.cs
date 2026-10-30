using System.Text;
using System.Text.Json.Nodes;

namespace Autonogrammer.Native.Tests.Send;

/// <summary>
/// A scripted <c>omp acp</c> peer. It answers the handshake and the session, answers each prompt with one text reply, and can raise a
/// permission request that the client decides or hold a prompt until the client cancels it. It records what the client sent.
/// </summary>
internal sealed class ScriptedAgent
{
    /// <summary>The session identifier the agent opens.</summary>
    public const string SessionId = "session-1";

    /// <summary>The request identifier of the permission request the agent raises.</summary>
    public const long PermissionRequestId = 900;

    private readonly System.Threading.Lock _gate = new();
    private readonly string _reply;
    private readonly bool _requestPermission;
    private readonly bool _holdPrompt;
    private readonly string _permissionKind;
    private readonly List<string> _promptTexts = [];
    private readonly List<string> _methods = [];
    private readonly List<string> _permissionOptionIds = [];
    private long _pendingPromptId;

    /// <summary>Creates an agent that answers each prompt with <paramref name="reply"/>.</summary>
    /// <param name="reply">The text the agent streams for each prompt.</param>
    /// <param name="requestPermission">Whether each prompt raises one permission request before it ends.</param>
    /// <param name="holdPrompt">Whether each prompt waits for the client to cancel it, instead of ending.</param>
    /// <param name="permissionKind">The tool kind of each permission request the agent raises.</param>
    public ScriptedAgent(string reply = "Hi from the scripted agent", bool requestPermission = false, bool holdPrompt = false, string permissionKind = "bash")
    {
        _reply = reply;
        _requestPermission = requestPermission;
        _holdPrompt = holdPrompt;
        _permissionKind = permissionKind;
    }

    /// <summary>Gets the text of every prompt the client sent, in order.</summary>
    public IReadOnlyList<string> PromptTexts
    {
        get
        {
            lock (_gate)
            {
                return _promptTexts.ToArray();
            }
        }
    }

    /// <summary>Gets the method of every client request and notification, in order.</summary>
    public IReadOnlyList<string> Methods
    {
        get
        {
            lock (_gate)
            {
                return _methods.ToArray();
            }
        }
    }

    /// <summary>Gets the option the client selected for each permission request, in order.</summary>
    public IReadOnlyList<string> PermissionOptionIds
    {
        get
        {
            lock (_gate)
            {
                return _permissionOptionIds.ToArray();
            }
        }
    }

    /// <summary>Finishes the held prompt as its turn ends on its own, streaming the reply and ending with <c>end_turn</c>.</summary>
    /// <returns>The frames to push to the client, or none when no prompt is pending.</returns>
    public IEnumerable<byte[]> FinishHeldPrompt()
    {
        lock (_gate)
        {
            return _pendingPromptId == 0 ? [] : [Encode(Update(_reply)), Encode(EndPrompt("end_turn"))];
        }
    }

    /// <summary>Answers one frame the client sent, and returns the frames the agent sends back.</summary>
    /// <param name="frame">The UTF-8 JSON-RPC frame the client sent.</param>
    /// <returns>The frames to deliver to the client, in order.</returns>
    public IEnumerable<byte[]> Respond(byte[] frame)
    {
        var node = JsonNode.Parse(frame)!.AsObject();
        lock (_gate)
        {
            return Answer(node).Select(Encode).ToArray();
        }
    }

    private List<JsonObject> Answer(JsonObject node)
    {
        var method = node["method"]?.GetValue<string>();
        if (method is null)
        {
            return AnswerClientResponse(node);
        }

        _methods.Add(method);
        var id = node["id"]?.GetValue<long>() ?? 0;
        switch (method)
        {
            case "initialize":
                return [Reply(id, Initialize())];
            case "session/new":
                return [Reply(id, new JsonObject { ["sessionId"] = SessionId })];
            case "session/prompt":
                _pendingPromptId = id;
                _promptTexts.Add(node["params"]!["prompt"]![0]!["text"]!.GetValue<string>());
                if (_holdPrompt)
                {
                    return [];
                }

                if (_requestPermission)
                {
                    return [PermissionRequest(_permissionKind)];
                }

                return [Update(_reply), EndPrompt("end_turn")];
            case "session/cancel":
                return _pendingPromptId == 0 ? [] : [EndPrompt("cancelled")];
            default:
                return node["id"] is null ? [] : [Reply(id, new JsonObject())];
        }
    }

    private List<JsonObject> AnswerClientResponse(JsonObject node)
    {
        var id = node["id"]?.GetValue<long>() ?? 0;
        if (id != PermissionRequestId)
        {
            return [];
        }

        _permissionOptionIds.Add(node["result"]?["outcome"]?["optionId"]?.GetValue<string>() ?? "cancelled");
        return _pendingPromptId == 0 ? [] : [EndPrompt("end_turn")];
    }

    /// <summary>Ends the pending prompt with its stop reason, and forgets it.</summary>
    private JsonObject EndPrompt(string stopReason)
    {
        var reply = Reply(_pendingPromptId, new JsonObject { ["stopReason"] = stopReason });
        _pendingPromptId = 0;
        return reply;
    }

    /// <summary>The capabilities of the captured omp acp handshake, so the client's capability flattening reads every block.</summary>
    private static JsonObject Initialize() => new()
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

    private static JsonObject Reply(long id, JsonObject result) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["result"] = result,
    };

    private static JsonObject Update(string text) => new()
    {
        ["jsonrpc"] = "2.0",
        ["method"] = "session/update",
        ["params"] = new JsonObject
        {
            ["sessionId"] = SessionId,
            ["update"] = new JsonObject
            {
                ["sessionUpdate"] = "agent_message_chunk",
                ["content"] = new JsonObject { ["type"] = "text", ["text"] = text },
            },
        },
    };

    private static JsonObject PermissionRequest(string kind) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = PermissionRequestId,
        ["method"] = "session/request_permission",
        ["params"] = new JsonObject
        {
            ["sessionId"] = SessionId,
            ["toolCall"] = new JsonObject { ["toolCallId"] = "call-1", ["title"] = kind, ["kind"] = kind },
            ["options"] = new JsonArray(
                new JsonObject { ["optionId"] = "allow", ["name"] = "Allow", ["kind"] = "allow_once" },
                new JsonObject { ["optionId"] = "reject", ["name"] = "Reject", ["kind"] = "reject_once" }),
        },
    };

    private static byte[] Encode(JsonObject frame) => Encoding.UTF8.GetBytes(frame.ToJsonString());
}
