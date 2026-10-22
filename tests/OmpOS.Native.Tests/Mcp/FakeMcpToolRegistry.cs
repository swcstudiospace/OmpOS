using System.Text.Json;
using OmpOS.Harness.Mcp;
using OmpOS.Native.Views.Mcp;

namespace OmpOS.Native.Tests.Mcp;

/// <summary>
/// An in-memory tool registry built from <see cref="McpToolDescriptor"/> models, with no MCP server behind it. It keeps the
/// production registry's rules: a registered tool is enabled for a session unless that session disabled it, and a tool that
/// is not registered cannot be enabled, disabled or offered.
/// </summary>
internal sealed class FakeMcpToolRegistry : IMcpToolSource
{
    private readonly IReadOnlyList<McpToolDescriptor> _tools;
    private readonly HashSet<(string SessionId, McpToolId Tool)> _disabled = [];

    /// <summary>Creates a registry that holds the given tools, all enabled for every session.</summary>
    /// <param name="tools">The registered tools.</param>
    public FakeMcpToolRegistry(IEnumerable<McpToolDescriptor> tools)
    {
        _tools = tools
            .OrderBy(tool => tool.Id.ServerName, StringComparer.Ordinal)
            .ThenBy(tool => tool.Id.ToolName, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<McpToolDescriptor> Tools => _tools;

    /// <summary>Builds the descriptor of one tool with an empty object schema.</summary>
    /// <param name="serverName">The registered server name.</param>
    /// <param name="toolName">The tool name the server advertises.</param>
    /// <param name="description">The advertised description, if any.</param>
    /// <returns>The tool descriptor.</returns>
    public static McpToolDescriptor Describe(string serverName, string toolName, string? description = null)
    {
        using var document = JsonDocument.Parse("{\"type\":\"object\"}");
        return new McpToolDescriptor(new McpToolId(serverName, toolName), description, document.RootElement.Clone());
    }

    /// <inheritdoc />
    public IReadOnlyList<McpToolDescriptor> GetOfferedTools(string sessionId) =>
        _tools.Where(tool => IsEnabled(sessionId, tool.Id)).ToList();

    /// <inheritdoc />
    public bool IsEnabled(string sessionId, McpToolId tool)
    {
        RequireRegistered(tool);
        return !_disabled.Contains((sessionId, tool));
    }

    /// <inheritdoc />
    public void Enable(string sessionId, McpToolId tool)
    {
        RequireRegistered(tool);
        _disabled.Remove((sessionId, tool));
    }

    /// <inheritdoc />
    public void Disable(string sessionId, McpToolId tool)
    {
        RequireRegistered(tool);
        _disabled.Add((sessionId, tool));
    }

    private void RequireRegistered(McpToolId tool)
    {
        if (!_tools.Any(registered => registered.Id == tool))
        {
            throw new ArgumentException("MCP tool '" + tool + "' is not registered.", nameof(tool));
        }
    }
}
