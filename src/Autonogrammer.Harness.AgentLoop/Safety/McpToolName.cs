namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>Reads the server and tool out of the name omp gives an MCP tool.</summary>
public static class McpToolName
{
    private const string Prefix = "mcp__";

    /// <summary>
    /// Parses <c>mcp__server__tool</c> and <c>mcp__server_tool</c>. In the second form the first underscore ends the server name, so a server name that
    /// contains an underscore is read wrongly. UNVERIFIED against omp: <c>omp acp</c> with an MCP server, a fake key and no model turn, reading the tool call's name.
    /// </summary>
    /// <param name="name">The tool's name.</param>
    /// <param name="server">The server's name, or an empty string.</param>
    /// <param name="tool">The tool's name, or an empty string.</param>
    /// <returns>True when the name has the MCP form.</returns>
    public static bool TryParse(string? name, out string server, out string tool)
    {
        server = string.Empty;
        tool = string.Empty;
        if (name is null || !name.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = name[Prefix.Length..];
        var split = rest.IndexOf("__", StringComparison.Ordinal);
        var separatorLength = 2;
        if (split < 0)
        {
            split = rest.IndexOf('_', StringComparison.Ordinal);
            separatorLength = 1;
        }

        if (split <= 0 || split + separatorLength >= rest.Length)
        {
            return false;
        }

        server = rest[..split];
        tool = rest[(split + separatorLength)..];
        return true;
    }
}
