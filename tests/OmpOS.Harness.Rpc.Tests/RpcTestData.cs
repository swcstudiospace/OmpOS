using System.Text;
using System.Text.Json.Nodes;

namespace OmpOS.Harness.Rpc.Tests;

/// <summary>Fixture loading and frame builders shared by the RPC tests.</summary>
internal static class RpcTestData
{
    /// <summary>The state object a healthy engine returns for <c>get_state</c> in these tests.</summary>
    public const string StateData = """{"model":{"id":"glm-5.3-flash","provider":"zai"},"sessionFile":"/tmp/s.jsonl"}""";

    /// <summary>Reads the committed six-line RPC capture.</summary>
    /// <returns>The lines in capture order.</returns>
    public static IReadOnlyList<string> Fixture() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TestData", "frames", "rpc-out.ndjson"));

    /// <summary>Builds a successful response line.</summary>
    /// <param name="id">The command id to echo.</param>
    /// <param name="command">The command type.</param>
    /// <param name="dataJson">The raw JSON of the <c>data</c> member.</param>
    /// <returns>The frame line.</returns>
    public static string Ok(string id, string command, string dataJson) =>
        "{\"id\":\"" + id + "\",\"type\":\"response\",\"command\":\"" + command + "\",\"success\":true,\"data\":" + dataJson + "}";

    /// <summary>Builds a rejected response line.</summary>
    /// <param name="id">The command id to echo.</param>
    /// <param name="command">The command type.</param>
    /// <param name="error">The failure text.</param>
    /// <returns>The frame line.</returns>
    public static string Rejected(string id, string command, string error) =>
        "{\"id\":\"" + id + "\",\"type\":\"response\",\"command\":\"" + command + "\",\"success\":false,\"error\":\"" + error + "\"}";

    /// <summary>Builds a <c>prompt_result</c> line.</summary>
    /// <param name="id">The admitted prompt's id.</param>
    /// <param name="status">The terminal status.</param>
    /// <param name="errorJson">Optional raw JSON of the <c>error</c> member.</param>
    /// <returns>The frame line.</returns>
    public static string PromptResult(string id, string status, string? errorJson = null)
    {
        var builder = new StringBuilder();
        builder.Append("{\"type\":\"prompt_result\",\"id\":\"").Append(id)
            .Append("\",\"agentInvoked\":true,\"status\":\"").Append(status).Append('"');
        if (errorJson is not null)
        {
            builder.Append(",\"error\":").Append(errorJson);
        }

        return builder.Append(",\"sessionSettled\":true}").ToString();
    }

    /// <summary>Builds a streamed text delta from the agent.</summary>
    /// <param name="delta">The text fragment.</param>
    /// <returns>The frame line.</returns>
    public static string TextDelta(string delta) =>
        "{\"type\":\"message_update\",\"messageId\":\"m1\",\"message\":{},\"assistantMessageEvent\":{\"type\":\"text_delta\",\"contentIndex\":0,\"delta\":\""
        + delta
        + "\"}}";

    /// <summary>Returns the value of the command's <c>id</c> member.</summary>
    /// <param name="commandLine">A command line written by the session.</param>
    /// <returns>The id text.</returns>
    public static string IdOf(string commandLine) =>
        JsonNode.Parse(commandLine)!["id"]!.GetValue<string>();

    /// <summary>Returns the value of the command's <c>type</c> member.</summary>
    /// <param name="commandLine">A command line written by the session.</param>
    /// <returns>The command type.</returns>
    public static string TypeOf(string commandLine) =>
        JsonNode.Parse(commandLine)!["type"]!.GetValue<string>();

    /// <summary>Rewrites a fixture response so it carries the id of the command that asked for it.</summary>
    /// <param name="fixtureLine">The captured response line.</param>
    /// <param name="id">The id the session assigned to the command.</param>
    /// <returns>The rewritten line.</returns>
    public static string WithId(string fixtureLine, string id)
    {
        JsonObject node = JsonNode.Parse(fixtureLine)!.AsObject();
        node["id"] = id;
        return node.ToJsonString();
    }

    /// <summary>Splits a logical frame into <paramref name="count"/> <c>rpc_chunk</c> lines in index order.</summary>
    /// <param name="logicalJson">The frame to chunk; its UTF-8 size must exceed 1 MiB.</param>
    /// <param name="chunkId">The shared chunk id.</param>
    /// <param name="count">The number of chunks.</param>
    /// <returns>The chunk lines, index 0 first.</returns>
    public static IReadOnlyList<string> ChunkLines(string logicalJson, string chunkId, int count)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(logicalJson);
        int size = (bytes.Length + count - 1) / count;
        var lines = new List<string>(count);
        for (int index = 0; index < count; index++)
        {
            int start = index * size;
            int length = Math.Min(size, bytes.Length - start);
            lines.Add(new JsonObject
            {
                ["type"] = "rpc_chunk",
                ["chunkId"] = chunkId,
                ["index"] = index,
                ["count"] = count,
                ["byteLength"] = bytes.Length,
                ["data"] = Convert.ToBase64String(bytes, start, length),
            }.ToJsonString());
        }

        return lines;
    }

    /// <summary>Builds a logical <c>get_state</c> response whose UTF-8 size exceeds 1 MiB.</summary>
    /// <param name="id">The command id to echo.</param>
    /// <returns>The logical frame JSON.</returns>
    public static string LargeStateResponse(string id) =>
        new JsonObject
        {
            ["id"] = id,
            ["type"] = "response",
            ["command"] = "get_state",
            ["success"] = true,
            ["data"] = new JsonObject
            {
                ["model"] = new JsonObject { ["id"] = "big-model", ["provider"] = "test" },
                ["padding"] = new string('p', 1_100_000),
            },
        }.ToJsonString();
}
