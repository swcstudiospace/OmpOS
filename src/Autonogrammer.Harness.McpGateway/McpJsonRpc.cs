using System.Buffers;
using System.Text.Json;

namespace Autonogrammer.Harness.McpGateway;

/// <summary>Writes the JSON-RPC 2.0 response envelopes the gateway returns. No reflection-based serialization is involved.</summary>
internal static class McpJsonRpc
{
    /// <summary>The body was not valid JSON.</summary>
    public const int ParseError = -32700;

    /// <summary>The JSON was not a valid JSON-RPC request object.</summary>
    public const int InvalidRequest = -32600;

    /// <summary>The method is not one the gateway serves.</summary>
    public const int MethodNotFound = -32601;

    /// <summary>The parameters are missing, malformed, or name a tool that is not granted.</summary>
    public const int InvalidParams = -32602;

    /// <summary>The Streamable HTTP session is unknown (the MCP transport uses this code with HTTP 404).</summary>
    public const int SessionNotFound = -32001;

    /// <summary>Builds a JSON-RPC error response.</summary>
    /// <param name="id">The request id to echo, or <c>null</c> when none could be determined.</param>
    /// <param name="code">The JSON-RPC error code.</param>
    /// <param name="message">A short fixed message; never request content.</param>
    /// <returns>The UTF-8 response body.</returns>
    public static ReadOnlyMemory<byte> Error(JsonElement? id, int code, string message)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WritePropertyName("id");
            if (id is { } value)
            {
                value.WriteTo(writer);
            }
            else
            {
                writer.WriteNullValue();
            }

            writer.WriteStartObject("error");
            writer.WriteNumber("code", code);
            writer.WriteString("message", message);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.WrittenMemory;
    }

    /// <summary>Builds a JSON-RPC success response.</summary>
    /// <param name="id">The request id to echo.</param>
    /// <param name="writeResult">Writes the value of the <c>result</c> property.</param>
    /// <returns>The UTF-8 response body.</returns>
    public static ReadOnlyMemory<byte> Result(JsonElement id, Action<Utf8JsonWriter> writeResult)
    {
        ArgumentNullException.ThrowIfNull(writeResult);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WritePropertyName("id");
            id.WriteTo(writer);
            writer.WritePropertyName("result");
            writeResult(writer);
            writer.WriteEndObject();
        }

        return buffer.WrittenMemory;
    }
}
