namespace Autonogrammer.Harness.Mcp;

/// <summary>Identifies one tool: the name a server was registered under and the tool name it advertises.</summary>
/// <param name="ServerName">The registered server name.</param>
/// <param name="ToolName">The tool name the server advertised.</param>
public readonly record struct McpToolId(string ServerName, string ToolName)
{
    /// <inheritdoc />
    public override string ToString() => ServerName + "/" + ToolName;
}
