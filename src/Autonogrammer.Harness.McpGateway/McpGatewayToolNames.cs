using Autonogrammer.Harness.Mcp;

namespace Autonogrammer.Harness.McpGateway;

/// <summary>
/// The names the gateway offers tools under. One gateway session sees tools from several upstream servers, so the
/// server name is part of the tool name; the two parts are joined with <see cref="Separator"/>, which the MCP tool-name
/// character set allows. The gateway never parses a name back: a call is matched against the composed names of the tools
/// granted to the calling session.
/// </summary>
public static class McpGatewayToolNames
{
    /// <summary>The text placed between the server name and the tool name.</summary>
    public const string Separator = "__";

    /// <summary>Composes the name the gateway offers a tool under.</summary>
    /// <param name="tool">The registered tool.</param>
    /// <returns>The server name, <see cref="Separator"/>, then the tool name.</returns>
    public static string Compose(McpToolId tool) => tool.ServerName + Separator + tool.ToolName;
}
