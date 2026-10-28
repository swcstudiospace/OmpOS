using System.Text;
using OmpOS.Harness.Acp;
using OmpOS.Harness.Mcp.Config;

namespace OmpOS.Harness.Mcp.Catalog;

/// <summary>
/// The registry facade the app asks about MCP servers: it loads the configuration files, applies the user's choices (switched off, approved)
/// and decides a <see cref="McpServerState"/> for each server. <see cref="OfferedServers"/> is the <c>mcpServers</c> array a session is created with.
/// A server is offered only when it is enabled, complete (every variable expanded) and, for a project server that starts a process or reads the
/// environment, approved for its exact declaration. A connection check is a live operation that runs only when the user asks.
/// </summary>
/// <remarks>
/// Not thread-safe: call it from one context, the one that owns the settings page. Every expected failure is an <see cref="McpCatalogException"/>
/// with a message that carries no secret. <see cref="Changed"/> is raised after each refresh, change and check.
/// </remarks>
public sealed class McpServerCatalog
{
    private const int MaxReasonLength = 200;
    private const int MinRedactedLength = 4;

    private readonly McpConfigLoader _loader;
    private readonly IMcpStateStore _stateStore;
    private readonly McpOwnedConfigWriter _writer;
    private readonly IMcpConnectionChecker? _checker;
    private readonly McpToolRegistry? _registry;
    private readonly Dictionary<string, CheckRecord> _checks = new(StringComparer.Ordinal);
    private McpLocatorContext? _context;
    private McpConfigSet _config = McpConfigSet.Empty;
    private McpState _state = McpState.Empty;

    /// <summary>Creates a catalog.</summary>
    /// <param name="files">The file tree configuration is read from and the owned file is written to.</param>
    /// <param name="stateStore">Where the user's choices are kept.</param>
    /// <param name="checker">Runs connection checks, or <see langword="null"/> when this host has none.</param>
    /// <param name="registry">The tool registry whose connected servers count as <see cref="McpServerState.Connected"/>, or <see langword="null"/>.</param>
    public McpServerCatalog(IMcpFileStore files, IMcpStateStore stateStore, IMcpConnectionChecker? checker = null, McpToolRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(stateStore);
        _loader = new McpConfigLoader(files);
        _writer = new McpOwnedConfigWriter(files);
        _stateStore = stateStore;
        _checker = checker;
        _registry = registry;
        Current = McpCatalogSnapshot.Empty;
    }

    /// <summary>Occurs after a refresh, a change or a connection check changed <see cref="Current"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the catalog as of the last refresh or change. It is empty until <see cref="RefreshAsync"/> runs.</summary>
    public McpCatalogSnapshot Current { get; private set; }

    /// <summary>Gets whether a connection check can run in this host.</summary>
    public bool CanCheck => _checker is not null;

    /// <summary>Reads the configuration files and the state again and decides every server's state.</summary>
    /// <param name="context">The roots, home and environment.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The new catalog.</returns>
    public async Task<McpCatalogSnapshot> RefreshAsync(McpLocatorContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var config = await _loader.LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var state = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        _context = context;
        _config = config;
        _state = state;
        return Rebuild();
    }

    /// <summary>Returns the entries of the <c>mcpServers</c> array to pass to <c>session/new</c>, <c>session/load</c>, <c>session/resume</c> and <c>session/fork</c>.</summary>
    /// <returns>The ACP entries of every offered server, in precedence order.</returns>
    public IReadOnlyList<AcpMcpServer> OfferedServers() =>
        AcpMcpServerMapper.Map(Current.Entries.Where(entry => entry.IsOffered).Select(entry => entry.Server));

    /// <summary>Switches a server on or off. The choice is kept in the state file and wins over the file's own <c>disabled</c> flag.</summary>
    /// <param name="name">The server name.</param>
    /// <param name="enabled">True to switch the server on.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the choice is saved.</returns>
    /// <exception cref="McpCatalogException">The server is unknown or the state file cannot be written.</exception>
    public async Task SetEnabledAsync(string name, bool enabled, CancellationToken cancellationToken)
    {
        _ = Find(name);
        await SaveAsync(_state.WithEnabled(name, enabled), cancellationToken).ConfigureAwait(false);
        _ = Rebuild();
    }

    /// <summary>Approves a server's exact declaration. Nothing is started.</summary>
    /// <param name="name">The server name.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the approval is saved.</returns>
    /// <exception cref="McpCatalogException">The server is unknown or the state file cannot be written.</exception>
    public async Task GrantConsentAsync(string name, CancellationToken cancellationToken)
    {
        var server = Find(name);
        await SaveAsync(_state.WithConsent(server.ConsentIdentity), cancellationToken).ConfigureAwait(false);
        _ = Rebuild();
    }

    /// <summary>Withdraws the approval of a server's exact declaration.</summary>
    /// <param name="name">The server name.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is saved.</returns>
    /// <exception cref="McpCatalogException">The server is unknown or the state file cannot be written.</exception>
    public async Task RevokeConsentAsync(string name, CancellationToken cancellationToken)
    {
        var server = Find(name);
        await SaveAsync(_state.WithoutConsent(server.ConsentIdentity), cancellationToken).ConfigureAwait(false);
        _ = Rebuild();
    }

    /// <summary>
    /// Runs a connection check for one server and keeps the result. A server that is switched off, incomplete or not approved is not checked:
    /// its status is returned as it is.
    /// </summary>
    /// <param name="name">The server name.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The server's status after the check.</returns>
    /// <exception cref="McpCatalogException">The server is unknown, or this host has no connection check.</exception>
    public async Task<McpServerStatus> CheckAsync(string name, CancellationToken cancellationToken)
    {
        var server = Find(name);
        if (_checker is null)
        {
            throw new McpCatalogException("This host cannot check MCP connections.");
        }

        var entry = Decide(server);
        if (!entry.IsOffered)
        {
            return entry.Status;
        }

        _checks[name] = new CheckRecord(server.ConsentIdentity, new McpServerStatus(McpServerState.Connecting));
        _ = Rebuild();

        McpConnectionResult result;
        try
        {
            result = await _checker.CheckAsync(server, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _ = _checks.Remove(name);
            _ = Rebuild();
            throw;
        }

        var status = result.Outcome switch
        {
            McpConnectionOutcome.Connected => new McpServerStatus(McpServerState.Connected, null, result.ToolCount),
            McpConnectionOutcome.AuthRequired => new McpServerStatus(McpServerState.AuthRequired),
            _ => new McpServerStatus(McpServerState.Failed, Redact(result.Reason, server)),
        };
        _checks[name] = new CheckRecord(server.ConsentIdentity, status);
        _ = Rebuild();
        return status;
    }

    /// <summary>Adds a placeholder server to the OmpOS-owned <c>.ompos/mcp.json</c> of the first workspace root and switches it off until the user has edited and enabled it.</summary>
    /// <param name="kind">The skeleton.</param>
    /// <param name="name">The server name.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The origin of the file that was written, to show.</returns>
    /// <exception cref="McpCatalogException">No workspace is open, the name is invalid or already configured anywhere, or a file cannot be changed.</exception>
    public async Task<string> AddTemplateAsync(McpTemplateKind kind, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        var context = _context;
        if (context is null || context.WorkspaceRoots.Count == 0 || string.IsNullOrWhiteSpace(context.WorkspaceRoots[0]))
        {
            throw new McpCatalogException("Open a workspace folder first: the new server is written to its .ompos/mcp.json.");
        }

        var taken = _config.Servers.FirstOrDefault(server => string.Equals(server.Name, name, StringComparison.Ordinal));
        if (taken is not null)
        {
            throw new McpCatalogException("A server named '" + name + "' is already configured in " + taken.Origin + ".");
        }

        var path = McpLocations.OwnedProjectFile(context.WorkspaceRoots[0]);
        await _writer.AddTemplateAsync(path, kind, name, cancellationToken).ConfigureAwait(false);
        await SaveAsync(_state.WithEnabled(name, false), cancellationToken).ConfigureAwait(false);
        _ = await RefreshAsync(context, cancellationToken).ConfigureAwait(false);
        return OwnedOrigin(context) ?? ".ompos/mcp.json";
    }

    private static string? OwnedOrigin(McpLocatorContext context) =>
        McpLocations.Enumerate(context)
            .FirstOrDefault(location => location.Scope == McpScope.Project && location.Format == McpConfigFormat.OmpOs)?.Origin;

    private McpServerSpec Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _config.Servers.FirstOrDefault(server => string.Equals(server.Name, name, StringComparison.Ordinal))
            ?? throw new McpCatalogException("No MCP server named '" + name + "' is configured.");
    }

    private async Task SaveAsync(McpState next, CancellationToken cancellationToken)
    {
        try
        {
            await _stateStore.SaveAsync(next, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new McpCatalogException("The MCP state file could not be written.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new McpCatalogException("The MCP state file could not be written.", exception);
        }

        _state = next;
    }

    private McpCatalogSnapshot Rebuild()
    {
        var entries = _config.Servers.Select(Decide).ToList();
        var owned = _context is null ? null : OwnedOrigin(_context);
        Current = new McpCatalogSnapshot(entries, _config.Files, _config.Diagnostics, _context is { WorkspaceRoots.Count: > 0 } ? owned : null);
        Changed?.Invoke(this, EventArgs.Empty);
        return Current;
    }

    private McpCatalogEntry Decide(McpServerSpec server)
    {
        var choice = _state.EnabledOverride(server.Name);
        var enabled = choice ?? server.EnabledInFile;
        var granted = server.RequiresConsent && _state.IsConsented(server.ConsentIdentity);
        if (!enabled)
        {
            var reason = choice == false ? "Switched off in OmpOS." : "Switched off in " + server.Origin + ".";
            return new McpCatalogEntry(server, new McpServerStatus(McpServerState.Disabled, reason), IsEnabled: false, IsOffered: false, granted);
        }

        if (!server.IsComplete)
        {
            return new McpCatalogEntry(server, new McpServerStatus(McpServerState.Failed, DescribeIncomplete(server)), IsEnabled: true, IsOffered: false, granted);
        }

        if (server.RequiresConsent && !granted)
        {
            return new McpCatalogEntry(server, new McpServerStatus(McpServerState.NeedsConsent), IsEnabled: true, IsOffered: false, granted);
        }

        return new McpCatalogEntry(server, ConnectedStatus(server) ?? new McpServerStatus(McpServerState.Configured), IsEnabled: true, IsOffered: true, granted);
    }

    private McpServerStatus? ConnectedStatus(McpServerSpec server)
    {
        if (_checks.TryGetValue(server.Name, out var record) && string.Equals(record.Identity, server.ConsentIdentity, StringComparison.Ordinal))
        {
            return record.Status;
        }

        var tools = _registry?.Tools.Count(tool => string.Equals(tool.Id.ServerName, server.Name, StringComparison.Ordinal)) ?? 0;
        return tools > 0 ? new McpServerStatus(McpServerState.Connected, null, tools) : null;
    }

    private static string DescribeIncomplete(McpServerSpec server)
    {
        if (server.UnresolvedVariables.Count == 0)
        {
            return server.Transport == McpTransportKind.Stdio ? "The entry has no command." : "The entry has no usable URL.";
        }

        var parts = server.UnresolvedVariables.Select(name =>
            name.StartsWith("env:", StringComparison.Ordinal) ? "environment variable " + name[4..]
            : name.StartsWith("input:", StringComparison.Ordinal) ? "an unsupported input (" + name + ")"
            : McpVariableInterpolator.IsVariableName(name) ? "environment variable " + name
            : "${" + name + "}");
        return "Not set: " + string.Join(", ", parts) + ".";
    }

    private static string Redact(string? reason, McpServerSpec server)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? "The server did not answer." : reason;
        var urls = server.Url is null ? Array.Empty<string>() : new[] { server.Url.AbsoluteUri, server.Url.OriginalString };
        var secrets = server.Env.Values
            .Concat(server.Headers.Values)
            .Concat(urls)
            .Where(value => value.Length >= MinRedactedLength)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(value => value.Length);
        foreach (var secret in secrets)
        {
            text = text.Replace(secret, "***", StringComparison.Ordinal);
        }

        var line = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            _ = line.Append(char.IsControl(character) ? ' ' : character);
        }

        var single = line.ToString().Trim();
        return single.Length <= MaxReasonLength ? single : single[..MaxReasonLength] + "...";
    }

    private sealed record CheckRecord(string Identity, McpServerStatus Status);
}
