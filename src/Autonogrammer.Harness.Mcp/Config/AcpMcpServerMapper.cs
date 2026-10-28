using Autonogrammer.Harness.Acp;

namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>
/// Maps configured servers to the entries of the ACP <c>mcpServers</c> array that <c>session/new</c>, <c>session/load</c>, <c>session/resume</c> and
/// <c>session/fork</c> carry. omp starts no MCP server of its own in an ACP session, so this array is the only way a configured server reaches a
/// model turn. A stdio server becomes <c>{name, command, args, env[]}</c>; an http or sse server becomes <c>{type, name, url, headers[]}</c>; the
/// name/value pairs are written in ordinal order by the ACP client.
/// </summary>
public static class AcpMcpServerMapper
{
    /// <summary>Maps servers that are complete, in the order given.</summary>
    /// <param name="servers">The servers to offer. Each must be <see cref="McpServerSpec.IsComplete"/>.</param>
    /// <returns>The ACP entries, in the order given.</returns>
    /// <exception cref="ArgumentException">A server is incomplete (an unexpanded variable or no command or URL). The message names the server and carries no value.</exception>
    public static IReadOnlyList<AcpMcpServer> Map(IEnumerable<McpServerSpec> servers)
    {
        ArgumentNullException.ThrowIfNull(servers);
        var mapped = new List<AcpMcpServer>();
        foreach (var server in servers)
        {
            mapped.Add(Map(server));
        }

        return mapped;
    }

    /// <summary>Maps one complete server.</summary>
    /// <param name="server">The server.</param>
    /// <returns>The ACP entry.</returns>
    /// <exception cref="ArgumentException">The server is incomplete.</exception>
    public static AcpMcpServer Map(McpServerSpec server)
    {
        ArgumentNullException.ThrowIfNull(server);
        if (!server.IsComplete)
        {
            throw new ArgumentException($"MCP server '{server.Name}' is incomplete and cannot be offered to a session.", nameof(server));
        }

        return server.Transport switch
        {
            McpTransportKind.Stdio => new AcpStdioMcpServer(server.Name, server.Command!, [.. server.Args], Copy(server.Env)),
            McpTransportKind.Http => new AcpHttpMcpServer(server.Name, server.Url!, Copy(server.Headers)),
            McpTransportKind.Sse => new AcpSseMcpServer(server.Name, server.Url!, Copy(server.Headers)),
            _ => throw new ArgumentException($"MCP server '{server.Name}' has an unknown transport.", nameof(server)),
        };
    }

    private static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string> source) =>
        new(source, StringComparer.Ordinal);
}
