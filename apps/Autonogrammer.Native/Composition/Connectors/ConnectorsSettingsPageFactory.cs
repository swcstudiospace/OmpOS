using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Connectors;
using Autonogrammer.Presentation.Settings.Cowork;

namespace Autonogrammer.Native.Composition.Connectors;

/// <summary>Builds the Connectors settings page from the head's MCP catalog, its connection check and the user's permission file.</summary>
internal static class ConnectorsSettingsPageFactory
{
    /// <summary>Creates the page. Each load lists the servers the catalog reads for the workspace.</summary>
    /// <param name="services">The head services that own the catalog and the connection check.</param>
    /// <param name="workspaceRoot">The workspace root the configuration files are read from.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is null, empty or white space.</exception>
    public static ConnectorsSettingsPage Create(HeadServices services, string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var store = new ConnectorPermissionStore(services.HomeDirectory);
        return new ConnectorsSettingsPage(
            store,
            cancellationToken => ReadServersAsync(services, workspaceRoot, cancellationToken),
            services.ConnectorChecks);
    }

    private static async Task<IReadOnlyList<ConnectorServerListing>> ReadServersAsync(HeadServices services, string workspaceRoot, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await services.Mcp.RefreshAsync(services.McpContext([workspaceRoot]), cancellationToken).ConfigureAwait(true);
            return [.. snapshot.Entries.Select(entry => new ConnectorServerListing(entry.Server.Name, entry.Server.Name))];
        }
        catch (McpCatalogException exception)
        {
            throw new SettingsSourceException(exception.Message, exception);
        }
    }
}
