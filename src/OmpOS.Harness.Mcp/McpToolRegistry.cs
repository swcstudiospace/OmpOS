using ModelContextProtocol.Client;

namespace OmpOS.Harness.Mcp;

/// <summary>
/// One registry of tools from every connected MCP server (stdio and Streamable HTTP alike).
/// Each tool is enabled or disabled per session; only enabled tools are offered to the model.
/// The registry owns the clients it is given and disposes them on <see cref="DisposeAsync"/>.
/// </summary>
public sealed class McpToolRegistry : IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly bool _enabledByDefault;
    private readonly Dictionary<string, McpClient> _clients = new(StringComparer.Ordinal);
    private readonly Dictionary<McpToolId, McpToolDescriptor> _tools = new();
    private readonly Dictionary<string, SessionOverrides> _sessions = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>Creates an empty registry.</summary>
    /// <param name="enabledByDefault">
    /// Whether a registered tool is enabled for a session that has no explicit setting for it.
    /// </param>
    public McpToolRegistry(bool enabledByDefault = true)
    {
        _enabledByDefault = enabledByDefault;
    }

    /// <summary>Every registered tool across all servers, ordered by server name then tool name.</summary>
    public IReadOnlyList<McpToolDescriptor> Tools
    {
        get
        {
            lock (_gate)
            {
                return SortedTools();
            }
        }
    }

    /// <summary>Lists the tools a connected server advertises and registers them under <paramref name="serverName"/>.</summary>
    /// <param name="serverName">The name the server is registered under; unique within the registry.</param>
    /// <param name="client">The connected client. The registry takes ownership and disposes it.</param>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>A task that completes once the server's tools are registered.</returns>
    /// <exception cref="InvalidOperationException">The server name is already registered, or a server advertised the same tool twice.</exception>
    /// <exception cref="ObjectDisposedException">The registry is disposed.</exception>
    public async Task RegisterServerAsync(string serverName, McpClient client, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentNullException.ThrowIfNull(client);

        lock (_gate)
        {
            ThrowIfDisposed();
            if (_clients.ContainsKey(serverName))
            {
                throw new InvalidOperationException("MCP server '" + serverName + "' is already registered.");
            }
        }

        var listed = await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        lock (_gate)
        {
            ThrowIfDisposed();
            if (_clients.ContainsKey(serverName))
            {
                throw new InvalidOperationException("MCP server '" + serverName + "' is already registered.");
            }

            var incoming = new Dictionary<McpToolId, McpToolDescriptor>();
            foreach (var tool in listed)
            {
                var id = new McpToolId(serverName, tool.Name);
                if (!incoming.TryAdd(id, Describe(id, tool)))
                {
                    throw new InvalidOperationException("MCP server '" + serverName + "' advertised tool '" + tool.Name + "' twice.");
                }
            }

            _clients.Add(serverName, client);
            foreach (var (id, descriptor) in incoming)
            {
                _tools.Add(id, descriptor);
            }
        }
    }

    /// <summary>Enables a tool for one session, overriding the registry default.</summary>
    /// <param name="sessionId">The session to change.</param>
    /// <param name="tool">A registered tool.</param>
    /// <exception cref="ArgumentException">The tool is not registered.</exception>
    public void Enable(string sessionId, McpToolId tool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_gate)
        {
            RequireRegistered(tool);
            var overrides = OverridesFor(sessionId);
            overrides.Disabled.Remove(tool);
            overrides.Enabled.Add(tool);
        }
    }

    /// <summary>Disables a tool for one session, overriding the registry default.</summary>
    /// <param name="sessionId">The session to change.</param>
    /// <param name="tool">A registered tool.</param>
    /// <exception cref="ArgumentException">The tool is not registered.</exception>
    public void Disable(string sessionId, McpToolId tool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_gate)
        {
            RequireRegistered(tool);
            var overrides = OverridesFor(sessionId);
            overrides.Enabled.Remove(tool);
            overrides.Disabled.Add(tool);
        }
    }

    /// <summary>Reports whether a registered tool is enabled for a session.</summary>
    /// <param name="sessionId">The session to ask about.</param>
    /// <param name="tool">The tool to check.</param>
    /// <returns><c>true</c> when the tool is registered and enabled for the session.</returns>
    public bool IsEnabled(string sessionId, McpToolId tool)
    {
        lock (_gate)
        {
            return _tools.ContainsKey(tool) && IsEnabledCore(sessionId, tool);
        }
    }

    /// <summary>The tools offered to the model for one session: the enabled tools only.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The enabled tools, ordered by server name then tool name.</returns>
    public IReadOnlyList<McpToolDescriptor> GetOfferedTools(string sessionId)
    {
        lock (_gate)
        {
            return SortedTools().Where(descriptor => IsEnabledCore(sessionId, descriptor.Id)).ToList();
        }
    }

    /// <summary>Drops the enable and disable settings of one session; later calls fall back to the registry default.</summary>
    /// <param name="sessionId">The session that ended.</param>
    public void EndSession(string sessionId)
    {
        lock (_gate)
        {
            _sessions.Remove(sessionId);
        }
    }

    /// <summary>Disposes every registered server client.</summary>
    /// <returns>A task that completes once every client is disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        List<McpClient> clients;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            clients = _clients.Values.ToList();
            _clients.Clear();
            _tools.Clear();
            _sessions.Clear();
        }

        foreach (var client in clients)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Looks up a tool that is enabled for the session.</summary>
    /// <param name="sessionId">The calling session.</param>
    /// <param name="tool">The tool being called.</param>
    /// <returns>The offered tool with its client.</returns>
    /// <exception cref="McpToolNotOfferedException">The tool is unknown or disabled for the session.</exception>
    internal McpOfferedTool ResolveOffered(string sessionId, McpToolId tool)
    {
        lock (_gate)
        {
            if (!_tools.TryGetValue(tool, out var descriptor) || !IsEnabledCore(sessionId, tool))
            {
                throw new McpToolNotOfferedException(sessionId, tool);
            }

            return new McpOfferedTool(descriptor, _clients[tool.ServerName]);
        }
    }

    private static McpToolDescriptor Describe(McpToolId id, McpClientTool tool) =>
        new(id, tool.ProtocolTool.Description, tool.ProtocolTool.InputSchema);

    private List<McpToolDescriptor> SortedTools() =>
        _tools.Values
            .OrderBy(descriptor => descriptor.Id.ServerName, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.Id.ToolName, StringComparer.Ordinal)
            .ToList();

    private bool IsEnabledCore(string sessionId, McpToolId tool)
    {
        if (!_sessions.TryGetValue(sessionId, out var overrides))
        {
            return _enabledByDefault;
        }

        if (overrides.Disabled.Contains(tool))
        {
            return false;
        }

        return overrides.Enabled.Contains(tool) || _enabledByDefault;
    }

    private SessionOverrides OverridesFor(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var overrides))
        {
            overrides = new SessionOverrides();
            _sessions.Add(sessionId, overrides);
        }

        return overrides;
    }

    private void RequireRegistered(McpToolId tool)
    {
        if (!_tools.ContainsKey(tool))
        {
            throw new ArgumentException("MCP tool '" + tool + "' is not registered.", nameof(tool));
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class SessionOverrides
    {
        public HashSet<McpToolId> Enabled { get; } = new();

        public HashSet<McpToolId> Disabled { get; } = new();
    }
}
