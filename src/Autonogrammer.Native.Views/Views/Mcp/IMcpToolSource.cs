using Autonogrammer.Harness.Mcp;

namespace Autonogrammer.Native.Views.Mcp;

/// <summary>
/// The tools the MCP view lists and toggles. Its members match <see cref="McpToolRegistry"/>, which
/// <see cref="McpToolRegistrySource"/> adapts; the view depends on this interface so that tests can supply a registry built
/// from the same descriptors without connecting MCP servers.
/// </summary>
internal interface IMcpToolSource
{
    /// <summary>Gets every registered tool, ordered by server name then tool name.</summary>
    IReadOnlyList<McpToolDescriptor> Tools { get; }

    /// <summary>Gets the tools enabled for a session, ordered by server name then tool name.</summary>
    /// <param name="sessionId">The session.</param>
    IReadOnlyList<McpToolDescriptor> GetOfferedTools(string sessionId);

    /// <summary>Returns whether a tool is enabled for a session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="tool">The tool.</param>
    bool IsEnabled(string sessionId, McpToolId tool);

    /// <summary>Enables a tool for a session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="tool">A registered tool.</param>
    void Enable(string sessionId, McpToolId tool);

    /// <summary>Disables a tool for a session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="tool">A registered tool.</param>
    void Disable(string sessionId, McpToolId tool);
}
