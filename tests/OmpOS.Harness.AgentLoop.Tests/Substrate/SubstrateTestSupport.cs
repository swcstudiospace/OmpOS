using System.Text;
using System.Text.Json;
using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Substrate;

namespace OmpOS.Harness.AgentLoop.Tests.Substrate;

/// <summary>Shared constants, fixtures and builders for the Substrate tests.</summary>
internal static class SubstrateTestSupport
{
    /// <summary>The Graph identifier used by the recorded fixtures.</summary>
    internal const string GraphId = "ut-lq1a2b3c-0f1e2d3c";

    /// <summary>The repository the recorded fixtures belong to.</summary>
    internal const string Repository = "swcstudiospace/OmpOS";

    /// <summary>A bearer token whose text must never appear in an exception, a log line, a stage or a result.</summary>
    internal const string Token = "tok-SECRET-4f9c2d71";

    /// <summary>Gets the endpoint every test uses. No socket is ever opened to it.</summary>
    internal static SubstrateEndpoint Endpoint { get; } = SubstrateEndpoint.Parse("http://127.0.0.1:7410");

    /// <summary>Reads a recorded fixture.</summary>
    /// <param name="name">The file name under <c>TestData/Substrate</c>.</param>
    /// <returns>The file text.</returns>
    internal static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Substrate", name));

    /// <summary>Builds a scope for a session, optionally with a Graph identifier and an omp session identifier.</summary>
    /// <param name="graphId">The Graph identifier, or null.</param>
    /// <param name="acpSessionId">The omp session identifier, or null.</param>
    /// <returns>The scope.</returns>
    internal static StageObservationScope Scope(string? graphId = GraphId, string? acpSessionId = "acp-session-1") =>
        new()
        {
            SessionId = "session-1",
            WorkspaceRoot = "/workspace/ompos",
            SessionStateDirectory = "/state/session-1",
            AcpSessionId = acpSessionId,
            GraphId = graphId,
        };

    /// <summary>Reads the tool name and arguments of a recorded <c>POST /mcp</c> request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The tool name and a detached copy of its arguments.</returns>
    internal static (string Name, JsonElement Arguments) ToolCall(RecordedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var document = JsonDocument.Parse(request.Body);
        var parameters = document.RootElement.GetProperty("params");
        return (parameters.GetProperty("name").GetString()!, parameters.GetProperty("arguments").Clone());
    }

    /// <summary>Builds a JSON-RPC reply that carries one text block.</summary>
    /// <param name="toolText">The text of the block.</param>
    /// <param name="isError">The tool error flag.</param>
    /// <returns>The reply as JSON text.</returns>
    internal static string RpcResult(string toolText, bool isError = false)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WriteNumber("id", 1);
            writer.WriteStartObject("result");
            writer.WriteStartArray("content");
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", toolText);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteBoolean("isError", isError);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Frames a JSON-RPC reply as a single server-sent event, as the streamable-HTTP transport does.</summary>
    /// <param name="json">The reply JSON.</param>
    /// <returns>The event stream text.</returns>
    internal static string Sse(string json) => "event: message\ndata: " + json + "\n\n";
}
