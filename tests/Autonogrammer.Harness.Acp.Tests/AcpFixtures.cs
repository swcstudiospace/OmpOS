using System.Collections.Frozen;
using System.Text;
using System.Text.Json.Nodes;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>Captured-frame fixtures and frame helpers shared by the ACP tests.</summary>
internal static class AcpFixtures
{
    /// <summary>The session identifier recorded in <c>acp-out.ndjson</c>.</summary>
    public const string CapturedSessionId = "01a1582d-a7c8-724b-a542-695b31c7d7b4";

    /// <summary>The working directory recorded in <c>acp-in.ndjson</c> (the checkout path at capture time; the frames are replayed byte for byte, so this is not renamed with the product).</summary>
    public const string CapturedCwd = "/root/src/repos/ompos";

    private static readonly FrozenDictionary<long, byte[]> CapturedResponses = Lines("acp-out.ndjson")
        .Select(Utf8)
        .ToFrozenDictionary(IdOf, static frame => frame);

    /// <summary>Reads a fixture file as non-empty lines.</summary>
    /// <param name="fileName">The file name under <c>TestData/frames</c>.</param>
    /// <returns>The lines, in file order.</returns>
    public static string[] Lines(string fileName) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TestData", "frames", fileName))
            .Where(static line => line.Length > 0)
            .ToArray();

    /// <summary>Encodes text as UTF-8.</summary>
    public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>Decodes a UTF-8 frame.</summary>
    public static string Text(byte[] frame) => Encoding.UTF8.GetString(frame);

    /// <summary>Returns the <c>method</c> of a frame, or <see langword="null"/> for responses.</summary>
    public static string? MethodOf(byte[] frame) => JsonNode.Parse(frame)?["method"]?.GetValue<string>();

    /// <summary>Returns the numeric <c>id</c> of a frame.</summary>
    public static long IdOf(byte[] frame) => JsonNode.Parse(frame)!["id"]!.GetValue<long>();

    /// <summary>
    /// Answers a client request. Requests the capture recorded (initialize) get the captured reply;
    /// <c>authenticate</c> is accepted with an empty result; <c>session/new</c> gets the captured reply
    /// re-addressed to the request id, because authentication shifts it past the captured id; every other
    /// request is acknowledged with an empty result. Responses and notifications get no reply.
    /// </summary>
    public static IEnumerable<byte[]> Reply(byte[] frame)
    {
        var node = JsonNode.Parse(frame)!;
        if (node["method"] is null || node["id"] is null)
        {
            return [];
        }

        var method = node["method"]!.GetValue<string>();
        var id = node["id"]!.GetValue<long>();
        if (string.Equals(method, "authenticate", StringComparison.Ordinal))
        {
            return [Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{}}}}")];
        }

        if (CapturedResponses.TryGetValue(id, out var captured))
        {
            return [captured];
        }

        if (string.Equals(method, "session/new", StringComparison.Ordinal))
        {
            var sessionNew = JsonNode.Parse(Text(CapturedResponses[2]))!;
            sessionNew["id"] = id;
            return [Utf8(sessionNew.ToJsonString())];
        }

        return [Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{}}}}")];
    }
}
