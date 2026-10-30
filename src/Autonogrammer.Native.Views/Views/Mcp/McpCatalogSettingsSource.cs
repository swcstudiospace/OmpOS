using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Harness.Mcp.Config;
using Autonogrammer.Harness.Mcp.Templates;
using Autonogrammer.Presentation.Mcp;

namespace Autonogrammer.Native.Views.Mcp;

/// <summary>
/// Adapts the harness <see cref="McpServerCatalog"/> to the framework-neutral <see cref="IMcpSettingsSource"/> the MCP page uses. The presentation
/// layer cannot reference the harness, so this class maps states, scopes and notices, and turns <see cref="McpCatalogException"/> into
/// <see cref="McpSourceException"/>. The adapter hands the page names, commands as written and hosts: never an environment value, a header value or a URL path.
/// </summary>
internal sealed class McpCatalogSettingsSource : IMcpSettingsSource
{
    private readonly McpServerCatalog _catalog;
    private readonly Func<McpLocatorContext> _context;
    private readonly Func<string, CancellationToken, Task>? _openFile;

    /// <summary>Creates the adapter.</summary>
    /// <param name="catalog">The catalog to show and change.</param>
    /// <param name="context">Returns the workspace roots, home directory and environment to read configuration with, as they are now.</param>
    /// <param name="openFile">Opens an absolute file path in the user's editor, or <see langword="null"/> when the host cannot.</param>
    public McpCatalogSettingsSource(McpServerCatalog catalog, Func<McpLocatorContext> context, Func<string, CancellationToken, Task>? openFile)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(context);
        _catalog = catalog;
        _context = context;
        _openFile = openFile;
    }

    /// <inheritdoc />
    public Task LoadAsync(CancellationToken cancellationToken) =>
        TranslateAsync(() => _catalog.RefreshAsync(_context(), cancellationToken));

    /// <inheritdoc />
    public McpSettingsSnapshot Snapshot()
    {
        var current = _catalog.Current;
        return new McpSettingsSnapshot(
            [.. current.Entries.Select(MapEntry)],
            [.. current.Files.Select(file => new McpFileEntry(file.Origin, MapScope(file.Scope), file.ServerCount, file.Problem))],
            [.. current.Diagnostics.Select(diagnostic => new McpNotice(MapSeverity(diagnostic.Severity), diagnostic.Origin, diagnostic.ServerName, diagnostic.Message))],
            current.OwnedConfigOrigin,
            _catalog.CanCheck);
    }

    /// <inheritdoc />
    public Task SetEnabledAsync(string name, bool enabled, CancellationToken cancellationToken) =>
        TranslateAsync(() => _catalog.SetEnabledAsync(name, enabled, cancellationToken));

    /// <inheritdoc />
    public Task GrantConsentAsync(string name, CancellationToken cancellationToken) =>
        TranslateAsync(() => _catalog.GrantConsentAsync(name, cancellationToken));

    /// <inheritdoc />
    public Task RevokeConsentAsync(string name, CancellationToken cancellationToken) =>
        TranslateAsync(() => _catalog.RevokeConsentAsync(name, cancellationToken));

    /// <inheritdoc />
    public Task CheckAsync(string name, CancellationToken cancellationToken) =>
        TranslateAsync(() => _catalog.CheckAsync(name, cancellationToken));

    /// <inheritdoc />
    public async Task<string> AddTemplateAsync(McpTemplateChoice choice, string name, CancellationToken cancellationToken)
    {
        var kind = choice == McpTemplateChoice.Http ? McpTemplateKind.Http : McpTemplateKind.Stdio;
        try
        {
            return await _catalog.AddTemplateAsync(kind, name, cancellationToken).ConfigureAwait(true);
        }
        catch (McpCatalogException exception)
        {
            throw new McpSourceException(exception.Message, exception);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<McpPresetChoice> Presets { get; } = [.. McpTemplateCatalog.Default.Select(MapPreset)];

    /// <inheritdoc />
    public async Task<string> AddPresetAsync(string presetId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presetId);
        try
        {
            return await _catalog.AddPresetAsync(presetId, null, cancellationToken).ConfigureAwait(true);
        }
        catch (McpCatalogException exception)
        {
            throw new McpSourceException(exception.Message, exception);
        }
    }

    /// <inheritdoc />
    public Task OpenConfigFileAsync(string origin, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (_openFile is null)
        {
            return Task.FromException(new McpSourceException("This host cannot open files."));
        }

        var location = McpLocations.Enumerate(_context())
            .FirstOrDefault(candidate => string.Equals(candidate.Origin, origin, StringComparison.Ordinal));
        return location is null
            ? Task.FromException(new McpSourceException("The file " + origin + " is no longer in the configuration."))
            : _openFile(location.Path, cancellationToken);
    }

    private static async Task TranslateAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (McpCatalogException exception)
        {
            throw new McpSourceException(exception.Message, exception);
        }
    }

    private static McpPresetChoice MapPreset(McpPreset preset) => new(
        preset.Id,
        preset.DisplayName,
        preset.Description,
        preset.Kind == McpTemplateKind.Http ? McpTemplateChoice.Http : McpTemplateChoice.Stdio,
        preset.DocumentationAddress,
        preset.Auth switch
        {
            McpPresetAuth.OAuth => McpCopy.AuthOAuth,
            McpPresetAuth.BearerToken => McpCopy.AuthBearerToken(preset.TokenVariable ?? string.Empty),
            _ => McpCopy.AuthNone,
        },
        preset.Note);

    private static McpServerEntry MapEntry(McpCatalogEntry entry)
    {
        var server = entry.Server;
        return new McpServerEntry(
            server.Name,
            server.Transport switch
            {
                McpTransportKind.Stdio => "stdio",
                McpTransportKind.Http => "http",
                _ => "sse",
            },
            MapScope(server.Scope),
            server.Origin,
            MapState(entry.Status.State),
            entry.Status.Reason,
            entry.Status.ToolCount,
            server.CommandPreview,
            server.EnvNames,
            server.HeaderNames,
            entry.IsEnabled,
            entry.IsOffered,
            server.RequiresConsent,
            entry.IsConsentGranted,
            server.HasOAuthSettings);
    }

    private static McpServerStatusKind MapState(McpServerState state) => state switch
    {
        McpServerState.NeedsConsent => McpServerStatusKind.NeedsConsent,
        McpServerState.Disabled => McpServerStatusKind.Disabled,
        McpServerState.Connecting => McpServerStatusKind.Connecting,
        McpServerState.Connected => McpServerStatusKind.Connected,
        McpServerState.Failed => McpServerStatusKind.Failed,
        McpServerState.AuthRequired => McpServerStatusKind.AuthRequired,
        _ => McpServerStatusKind.Configured,
    };

    private static McpEntryScope MapScope(McpScope scope) => scope == McpScope.Project ? McpEntryScope.Project : McpEntryScope.User;

    private static McpNoticeSeverity MapSeverity(McpDiagnosticSeverity severity) => severity switch
    {
        McpDiagnosticSeverity.Error => McpNoticeSeverity.Error,
        McpDiagnosticSeverity.Warning => McpNoticeSeverity.Warning,
        _ => McpNoticeSeverity.Info,
    };
}
