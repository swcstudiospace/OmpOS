using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Presentation.Settings.Connectors;

namespace Autonogrammer.Native.Composition.Connectors;

/// <summary>
/// Checks one server through <see cref="McpServerCatalog.CheckAsync"/>, so a disabled, unapproved or incomplete server is not contacted,
/// and reads the tool names from <see cref="RecordingConnectionChecker"/>.
/// </summary>
public sealed class ConnectorCheckSource : IConnectorCheckSource
{
    private readonly McpServerCatalog _catalog;
    private readonly RecordingConnectionChecker _recording;

    /// <summary>Creates a source.</summary>
    /// <param name="catalog">The catalog whose consent gate applies.</param>
    /// <param name="recording">The checker the catalog was constructed with.</param>
    public ConnectorCheckSource(McpServerCatalog catalog, RecordingConnectionChecker recording)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(recording);
        _catalog = catalog;
        _recording = recording;
    }

    /// <inheritdoc />
    public async Task<ConnectorCheckSnapshot> CheckAsync(string serverId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        var status = await _catalog.CheckAsync(serverId, cancellationToken).ConfigureAwait(false);
        return new ConnectorCheckSnapshot(status.State.ToString(), _recording.ToolNames(serverId));
    }
}
