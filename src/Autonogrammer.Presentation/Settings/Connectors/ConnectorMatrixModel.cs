namespace Autonogrammer.Presentation.Settings.Connectors;

/// <summary>
/// Maps Always allow, Needs approval and Blocked to <c>Mcp(server:tool)</c> allow and deny tokens and back.
/// A server-level token is <c>Mcp(server:*)</c>. Blocked denies every tool of that server. Always allow allows every tool unless a per-tool deny exists.
/// </summary>
public sealed class ConnectorMatrixModel
{
    /// <summary>The sentence the connectors page shows. Auto never trusts <c>readOnlyHint</c>.</summary>
    public const string ReadOnlyHintNotice = "Auto does not trust readOnlyHint.";

    private readonly IConnectorPermissionStore _store;

    /// <summary>Creates a matrix over a permission store.</summary>
    /// <param name="store">Where tokens are read and written.</param>
    public ConnectorMatrixModel(IConnectorPermissionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>Reads the effective policy of a server.</summary>
    /// <param name="serverId">The server id.</param>
    /// <returns>The server policy. No token is Needs approval.</returns>
    public ConnectorToolState ServerState(string serverId) => TokenState(serverId, "*");

    /// <summary>Reads the effective policy of a tool, after the server-level override.</summary>
    /// <param name="serverId">The server id.</param>
    /// <param name="tool">The tool name.</param>
    /// <returns>The policy the card shows.</returns>
    public ConnectorToolState ToolState(string serverId, string tool)
    {
        var server = ServerState(serverId);
        if (server == ConnectorToolState.Blocked)
        {
            return ConnectorToolState.Blocked;
        }

        var own = TokenState(serverId, tool);
        if (own == ConnectorToolState.Blocked)
        {
            return ConnectorToolState.Blocked;
        }

        return server == ConnectorToolState.AlwaysAllow ? ConnectorToolState.AlwaysAllow : own;
    }

    /// <summary>Writes a server-level policy. Per-tool tokens are left in place.</summary>
    /// <param name="serverId">The server id.</param>
    /// <param name="state">The policy.</param>
    public void SetServerState(string serverId, ConnectorToolState state) => SetToken(serverId, "*", state);

    /// <summary>Writes a per-tool policy.</summary>
    /// <param name="serverId">The server id.</param>
    /// <param name="tool">The tool name.</param>
    /// <param name="state">The policy.</param>
    public void SetToolState(string serverId, string tool, ConnectorToolState state) => SetToken(serverId, tool, state);

    /// <summary>Builds cards from server identity and the tokens currently stored. Tools are empty until a check supplies names.</summary>
    /// <param name="servers">The servers to show.</param>
    /// <returns>One card per server.</returns>
    public IReadOnlyList<ConnectorCardModel> Cards(IReadOnlyList<ConnectorServerListing> servers)
    {
        ArgumentNullException.ThrowIfNull(servers);
        return [.. servers.Select(server => new ConnectorCardModel(server.ServerId, server.DisplayName, ServerState(server.ServerId)))];
    }

    /// <summary>Builds tool rows for names a check listed, using the effective policy.</summary>
    /// <param name="serverId">The server id.</param>
    /// <param name="toolNames">The tool names.</param>
    /// <returns>The rows.</returns>
    public IReadOnlyList<ConnectorToolRow> Rows(string serverId, IReadOnlyList<string> toolNames)
    {
        ArgumentNullException.ThrowIfNull(toolNames);
        return [.. toolNames.Select(name => new ConnectorToolRow(serverId, name, ToolState(serverId, name)))];
    }

    /// <summary>Prints the notice only.</summary>
    /// <returns>The notice.</returns>
    public override string ToString() => ReadOnlyHintNotice;

    private ConnectorToolState TokenState(string serverId, string tool)
    {
        var token = Token(serverId, tool);
        if (Contains(_store.ReadDenyTokens(), token))
        {
            return ConnectorToolState.Blocked;
        }

        if (Contains(_store.ReadAllowTokens(), token))
        {
            return ConnectorToolState.AlwaysAllow;
        }

        return ConnectorToolState.NeedsApproval;
    }

    private void SetToken(string serverId, string tool, ConnectorToolState state)
    {
        var token = Token(serverId, tool);
        var allow = _store.ReadAllowTokens().Where(item => !Same(item, token)).ToList();
        var deny = _store.ReadDenyTokens().Where(item => !Same(item, token)).ToList();
        if (state == ConnectorToolState.AlwaysAllow)
        {
            allow.Add(token);
        }
        else if (state == ConnectorToolState.Blocked)
        {
            deny.Add(token);
        }

        _store.ReplaceAllowTokens(allow);
        _store.ReplaceDenyTokens(deny);
    }

    private static string Token(string serverId, string tool) => "Mcp(" + serverId + ":" + tool + ")";

    private static bool Contains(IReadOnlyList<string> tokens, string token) => tokens.Any(item => Same(item, token));

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.Ordinal);
}
