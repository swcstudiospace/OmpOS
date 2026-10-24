using System.Text;
using System.Text.Json.Nodes;

namespace OmpOS.Harness.Agent.Tests;

/// <summary>
/// Answers the ACP handshake from the captured <c>acp-out.ndjson</c> responses: the <c>initialize</c> result (id 1) and the
/// <c>session/new</c> result (id 2). Every other request, including <c>authenticate</c>, is answered with a synthesised empty
/// result: no <c>authenticate</c> response is recorded, so that empty result is not observed omp output. Each reply carries
/// the id of its request. The capture holds no prompt turn, so this responder emits no session events.
/// </summary>
internal sealed class RecordedAcpHandshake
{
    private const long InitializeResponseId = 1;
    private const long SessionNewResponseId = 2;
    private readonly string _initialize;
    private readonly string _sessionNew;

    private RecordedAcpHandshake(List<string> capturedResponses)
    {
        _initialize = CapturedWithId(capturedResponses, InitializeResponseId);
        _sessionNew = CapturedWithId(capturedResponses, SessionNewResponseId);
    }

    /// <summary>Loads the handshake responses from a capture in the linked transcript folder.</summary>
    /// <param name="fileName">The capture file name under <c>TestData/frames</c>.</param>
    public static RecordedAcpHandshake Load(string fileName) => new(TranscriptFiles.ReadLines(fileName));

    /// <summary>Answers one frame the session sent.</summary>
    /// <param name="frame">The frame bytes.</param>
    /// <returns>The frames to deliver. Responses and notifications from the session get none.</returns>
    public List<byte[]> Respond(byte[] frame)
    {
        JsonObject request = JsonNode.Parse(frame)!.AsObject();
        string? method = request["method"]?.GetValue<string>();
        if (method is null)
        {
            return [];
        }

        long id = request["id"]!.GetValue<long>();
        if (string.Equals(method, "initialize", StringComparison.Ordinal))
        {
            return [Utf8(WithId(_initialize, id))];
        }

        if (string.Equals(method, "session/new", StringComparison.Ordinal))
        {
            return [Utf8(WithId(_sessionNew, id))];
        }

        return [Utf8(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = new JsonObject() }.ToJsonString())];
    }

    private static string CapturedWithId(List<string> lines, long id)
    {
        foreach (string line in lines)
        {
            if (JsonNode.Parse(line)!["id"]?.GetValue<long>() == id)
            {
                return line;
            }
        }

        throw new InvalidOperationException($"The ACP capture has no response with id {id}.");
    }

    private static string WithId(string line, long id)
    {
        JsonObject node = JsonNode.Parse(line)!.AsObject();
        node["id"] = id;
        return node.ToJsonString();
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
