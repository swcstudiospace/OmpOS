using OmpOS.Harness.Mcp;

namespace OmpOS.Native.Views.Mcp;

/// <summary>
/// The MCP tools of every connected server, grouped by server, with the enable state of each tool for one session, and the
/// list of tools offered to the model for that session. Toggling a tool changes the session at once.
/// </summary>
internal sealed class McpToolsModel
{
    /// <summary>
    /// The session the tools view toggles tools for. The view has no session picker, so every toggle in it uses this one id.
    /// </summary>
    public const string DefaultSessionId = "workspace-mcp-tools";

    private readonly IMcpToolSource _source;
    private IReadOnlyList<McpServerGroup> _servers = [];

    /// <summary>Creates the model over a tool source and reads the tools once.</summary>
    /// <param name="source">The tool source.</param>
    /// <param name="sessionId">The session to toggle tools for.</param>
    public McpToolsModel(IMcpToolSource source, string sessionId = DefaultSessionId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        _source = source;
        SessionId = sessionId;
        Reload();
    }

    /// <summary>Raised after a toggle changes which tools are offered.</summary>
    public event EventHandler? OfferedChanged;

    /// <summary>Gets the session the toggles apply to.</summary>
    public string SessionId { get; }

    /// <summary>Gets the registered tools, one group per server, in server and tool name order.</summary>
    public IReadOnlyList<McpServerGroup> Servers => _servers;

    /// <summary>Gets the tools currently offered to the model for the session, in server and tool name order.</summary>
    public IReadOnlyList<McpOfferedRow> Offered =>
        _source.GetOfferedTools(SessionId).Select(tool => new McpOfferedRow(tool.Id)).ToList();

    /// <summary>Reads the registered tools and their enable state again.</summary>
    public void Reload()
    {
        _servers = _source.Tools
            .GroupBy(tool => tool.Id.ServerName, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new McpServerGroup(group.Key, RowsFor(group)))
            .ToList();
    }

    internal void SetEnabled(McpToolId tool, bool enabled)
    {
        if (enabled)
        {
            _source.Enable(SessionId, tool);
        }
        else
        {
            _source.Disable(SessionId, tool);
        }

        OfferedChanged?.Invoke(this, EventArgs.Empty);
    }

    private List<McpToolRow> RowsFor(IGrouping<string, McpToolDescriptor> group) =>
        group
            .OrderBy(tool => tool.Id.ToolName, StringComparer.Ordinal)
            .Select(tool => new McpToolRow(this, tool.Id, tool.Description, _source.IsEnabled(SessionId, tool.Id)))
            .ToList();
}
