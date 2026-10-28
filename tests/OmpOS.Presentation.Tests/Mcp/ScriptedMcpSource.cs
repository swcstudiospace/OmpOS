using OmpOS.Presentation.Mcp;

namespace OmpOS.Presentation.Tests.Mcp;

/// <summary>
/// An MCP source the test scripts: it holds servers, files and notices in memory, applies switch, consent and template writes to them,
/// records every call, and can hold a load or a check open or fail a load or a write with the expected failure.
/// </summary>
internal sealed class ScriptedMcpSource : IMcpSettingsSource
{
    private readonly List<McpServerEntry> _servers = [];
    private readonly TaskCompletionSource _checkGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _holdCheck;

    /// <summary>Creates a source over servers; the files are derived from their origins.</summary>
    /// <param name="servers">The servers in precedence order.</param>
    internal ScriptedMcpSource(params McpServerEntry[] servers)
    {
        _servers.AddRange(servers);
    }

    /// <summary>Gets or sets the notices the snapshot carries.</summary>
    internal List<McpNotice> Notices { get; set; } = [];

    /// <summary>Gets or sets the files the snapshot carries, or null to derive them from the servers.</summary>
    internal List<McpFileEntry>? Files { get; set; }

    /// <summary>Gets or sets the reason the next loads fail with, or null.</summary>
    internal string? LoadFailure { get; set; }

    /// <summary>Gets or sets the reason the next writes fail with, or null.</summary>
    internal string? WriteFailure { get; set; }

    /// <summary>Gets or sets the origin of the owned file, or null when no workspace is open.</summary>
    internal string? OwnedOrigin { get; set; } = ".ompos/mcp.json";

    /// <summary>Gets or sets whether the host can check connections.</summary>
    internal bool CanCheck { get; set; } = true;

    /// <summary>Gets the calls that reached the source, as <c>verb name</c> strings, in order.</summary>
    internal List<string> Calls { get; } = [];

    /// <summary>Makes the next check wait until <see cref="CompleteCheck"/>.</summary>
    internal void HoldCheck() => _holdCheck = true;

    /// <summary>Lets a held check complete.</summary>
    internal void CompleteCheck() => _checkGate.TrySetResult();

    /// <summary>Returns a server as the source holds it.</summary>
    /// <param name="name">The server name.</param>
    /// <returns>The entry.</returns>
    internal McpServerEntry Entry(string name) => _servers.Single(server => string.Equals(server.Name, name, StringComparison.Ordinal));

    /// <inheritdoc />
    public Task LoadAsync(CancellationToken cancellationToken)
    {
        Calls.Add("load");
        return LoadFailure is null ? Task.CompletedTask : Task.FromException(new McpSourceException(LoadFailure));
    }

    /// <inheritdoc />
    public McpSettingsSnapshot Snapshot()
    {
        var files = Files ?? _servers
            .GroupBy(server => server.Origin, StringComparer.Ordinal)
            .Select(group => new McpFileEntry(group.Key, group.First().Scope, group.Count(), null))
            .ToList();
        return new McpSettingsSnapshot([.. _servers], files, [.. Notices], OwnedOrigin, CanCheck);
    }

    /// <inheritdoc />
    public Task SetEnabledAsync(string name, bool enabled, CancellationToken cancellationToken)
    {
        Calls.Add((enabled ? "enable " : "disable ") + name);
        return WriteAsync(() => Replace(name, entry => entry with
        {
            IsEnabled = enabled,
            IsOffered = enabled && entry.IsOffered,
            Status = enabled ? McpServerStatusKind.Configured : McpServerStatusKind.Disabled,
            Reason = enabled ? null : "Switched off in OmpOS.",
        }));
    }

    /// <inheritdoc />
    public Task GrantConsentAsync(string name, CancellationToken cancellationToken)
    {
        Calls.Add("grant " + name);
        return WriteAsync(() => Replace(name, entry => entry with { Status = McpServerStatusKind.Configured, IsOffered = true, IsConsentGranted = true }));
    }

    /// <inheritdoc />
    public Task RevokeConsentAsync(string name, CancellationToken cancellationToken)
    {
        Calls.Add("revoke " + name);
        return WriteAsync(() => Replace(name, entry => entry with { Status = McpServerStatusKind.NeedsConsent, IsOffered = false, IsConsentGranted = false }));
    }

    /// <inheritdoc />
    public async Task CheckAsync(string name, CancellationToken cancellationToken)
    {
        Calls.Add("check " + name);
        if (_holdCheck)
        {
            await _checkGate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        await WriteAsync(() => Replace(name, entry => entry with { Status = McpServerStatusKind.Connected, ToolCount = 2 })).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public Task<string> AddTemplateAsync(McpTemplateChoice choice, string name, CancellationToken cancellationToken)
    {
        Calls.Add("template " + choice + " " + name);
        if (WriteFailure is not null)
        {
            return Task.FromException<string>(new McpSourceException(WriteFailure));
        }

        _servers.Add(new McpServerEntry(
            name, choice == McpTemplateChoice.Http ? "http" : "stdio", McpEntryScope.Project, ".ompos/mcp.json",
            McpServerStatusKind.Disabled, "Switched off in OmpOS.", null, "placeholder", [], [], false, false, true, false, false));
        return Task.FromResult(".ompos/mcp.json");
    }

    /// <summary>Gets the presets the source offers; add to it to script the add-from-preset list.</summary>
    internal List<McpPresetChoice> PresetList { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<McpPresetChoice> Presets => PresetList;

    /// <inheritdoc />
    public Task<string> AddPresetAsync(string presetId, CancellationToken cancellationToken)
    {
        Calls.Add("preset " + presetId);
        if (WriteFailure is not null)
        {
            return Task.FromException<string>(new McpSourceException(WriteFailure));
        }

        if (!PresetList.Exists(preset => string.Equals(preset.Id, presetId, StringComparison.Ordinal)))
        {
            return Task.FromException<string>(new McpSourceException("There is no MCP preset named '" + presetId + "'."));
        }

        var taken = _servers.Find(server => string.Equals(server.Name, presetId, StringComparison.Ordinal));
        if (taken is not null)
        {
            return Task.FromException<string>(new McpSourceException("A server named '" + presetId + "' is already configured in " + taken.Origin + "."));
        }

        _servers.Add(new McpServerEntry(
            presetId, "http", McpEntryScope.Project, ".ompos/mcp.json",
            McpServerStatusKind.Disabled, "Switched off in OmpOS.", null, "https://preset.example.com", [], [], false, false, true, false, false));
        return Task.FromResult(".ompos/mcp.json");
    }

    /// <inheritdoc />
    public Task OpenConfigFileAsync(string origin, CancellationToken cancellationToken)
    {
        Calls.Add("open " + origin);
        return WriteFailure is null ? Task.CompletedTask : Task.FromException(new McpSourceException(WriteFailure));
    }

    private Task WriteAsync(Action change)
    {
        if (WriteFailure is not null)
        {
            return Task.FromException(new McpSourceException(WriteFailure));
        }

        change();
        return Task.CompletedTask;
    }

    private void Replace(string name, Func<McpServerEntry, McpServerEntry> change)
    {
        var index = _servers.FindIndex(server => string.Equals(server.Name, name, StringComparison.Ordinal));
        _servers[index] = change(_servers[index]);
    }
}
