namespace Autonogrammer.Native.Views.Mcp;

/// <summary>The tools of one MCP server, as one group of the tools list.</summary>
/// <param name="ServerName">The registered server name.</param>
/// <param name="Tools">The server's tools, in tool name order.</param>
internal sealed record McpServerGroup(string ServerName, IReadOnlyList<McpToolRow> Tools);
