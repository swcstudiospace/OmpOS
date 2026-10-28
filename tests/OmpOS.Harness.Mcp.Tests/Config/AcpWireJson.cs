using System.Buffers;
using System.Text;
using System.Text.Json;
using OmpOS.Harness.Acp;

namespace OmpOS.Harness.Mcp.Tests.Config;

/// <summary>
/// Writes ACP <c>mcpServers</c> entries in the wire shape the ACP client sends (<c>AcpWireMapping</c> in OmpOS.Harness.Acp, which is internal): stdio is
/// <c>{name, command, args, env:[{name,value}]}</c> and http or sse is <c>{type, name, url, headers:[{name,value}]}</c>, pairs in ordinal name order.
/// </summary>
internal static class AcpWireJson
{
    /// <summary>Renders the entries as compact JSON.</summary>
    /// <param name="servers">The entries.</param>
    /// <returns>The JSON array.</returns>
    public static string Render(IReadOnlyList<AcpMcpServer> servers)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var server in servers)
            {
                writer.WriteStartObject();
                switch (server)
                {
                    case AcpStdioMcpServer stdio:
                        writer.WriteString("name", stdio.Name);
                        writer.WriteString("command", stdio.Command);
                        writer.WriteStartArray("args");
                        foreach (var argument in stdio.Args)
                        {
                            writer.WriteStringValue(argument);
                        }

                        writer.WriteEndArray();
                        WritePairs(writer, "env", stdio.Env);
                        break;
                    case AcpHttpMcpServer http:
                        WriteRemote(writer, "http", http.Name, http.Url, http.Headers);
                        break;
                    case AcpSseMcpServer sse:
                        WriteRemote(writer, "sse", sse.Name, sse.Url, sse.Headers);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown ACP MCP server type.");
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteRemote(Utf8JsonWriter writer, string type, string name, Uri url, IReadOnlyDictionary<string, string> headers)
    {
        writer.WriteString("type", type);
        writer.WriteString("name", name);
        writer.WriteString("url", url.AbsoluteUri);
        WritePairs(writer, "headers", headers);
    }

    private static void WritePairs(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, string> pairs)
    {
        writer.WriteStartArray(name);
        foreach (var pair in pairs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("name", pair.Key);
            writer.WriteString("value", pair.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
