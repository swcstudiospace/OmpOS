using OmpOS.Presentation.Mcp;

namespace OmpOS.Native.Tests.Mcp;

/// <summary>An MCP source the view tests script: servers in memory, approval and template writes applied to them, every call recorded. Every task completes at once.</summary>
internal sealed class ScriptedMcpPageSource : IMcpSettingsSource
{
    private readonly List<McpServerEntry> _servers = [];

    /// <summary>Creates a source over servers.</summary>
    /// <param name="servers">The servers in precedence order.</param>
    public ScriptedMcpPageSource(params McpServerEntry[] servers)
    {
        _servers.AddRange(servers);
    }

    /// <summary>Gets or sets the reason loads fail with, or null.</summary>
    public string? LoadFailure { get; set; }

    /// <summary>Gets or sets the notices the snapshot carries.</summary>
    public List<McpNotice> Notices { get; } = [];

    /// <summary>Gets the calls that reached the source, in order.</summary>
    public List<string> Calls { get; } = [];

    /// <inheritdoc />
    public Task LoadAsync(CancellationToken cancellationToken) =>
        LoadFailure is null ? Task.CompletedTask : Task.FromException(new McpSourceException(LoadFailure));

    /// <inheritdoc />
    public McpSettingsSnapshot Snapshot()
    {
        var files = _servers
            .GroupBy(server => server.Origin, StringComparer.Ordinal)
            .Select(group => new McpFileEntry(group.Key, group.First().Scope, group.Count(), null))
            .ToList();
        return new McpSettingsSnapshot([.. _servers], files, [.. Notices], ".ompos/mcp.json", CanCheck: true);
    }

    /// <inheritdoc />
    public Task SetEnabledAsync(string name, bool enabled, CancellationToken cancellationToken)
    {
        Calls.Add((enabled ? "enable " : "disable ") + name);
        Replace(name, entry => entry with { IsEnabled = enabled, Status = enabled ? McpServerStatusKind.Configured : McpServerStatusKind.Disabled });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task GrantConsentAsync(string name, CancellationToken cancellationToken)
    {
        Calls.Add("grant " + name);
        Replace(name, entry => entry with { Status = McpServerStatusKind.Configured, IsOffered = true, IsConsentGranted = true });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RevokeConsentAsync(string name, CancellationToken cancellationToken)
    {
        Calls.Add("revoke " + name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CheckAsync(string name, CancellationToken cancellationToken)
    {
        Calls.Add("check " + name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string> AddTemplateAsync(McpTemplateChoice choice, string name, CancellationToken cancellationToken)
    {
        Calls.Add("template " + choice + " " + name);
        _servers.Add(new McpServerEntry(name, "stdio", McpEntryScope.Project, ".ompos/mcp.json", McpServerStatusKind.Disabled, "Switched off in OmpOS.", null, "npx -y replace-with-mcp-server-package", [], [], false, false, true, false, false));
        return Task.FromResult(".ompos/mcp.json");
    }

    /// <inheritdoc />
    public IReadOnlyList<McpPresetChoice> Presets { get; } = [];

    /// <inheritdoc />
    public Task<string> AddPresetAsync(string presetId, CancellationToken cancellationToken)
    {
        Calls.Add("preset " + presetId);
        return Task.FromException<string>(new McpSourceException("This scripted page has no presets."));
    }

    /// <inheritdoc />
    public Task OpenConfigFileAsync(string origin, CancellationToken cancellationToken)
    {
        Calls.Add("open " + origin);
        return Task.CompletedTask;
    }

    private void Replace(string name, Func<McpServerEntry, McpServerEntry> change)
    {
        var index = _servers.FindIndex(server => string.Equals(server.Name, name, StringComparison.Ordinal));
        _servers[index] = change(_servers[index]);
    }
}
