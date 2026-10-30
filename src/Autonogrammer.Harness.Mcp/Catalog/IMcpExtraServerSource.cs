using Autonogrammer.Harness.Mcp.Config;

namespace Autonogrammer.Harness.Mcp.Catalog;

/// <summary>
/// Servers that do not come from the configuration files <see cref="McpServerCatalog"/> reads.
/// They are appended after every file server and are never written back into the user's mcp.json.
/// </summary>
public interface IMcpExtraServerSource
{
    /// <summary>Returns the extra servers for this refresh.</summary>
    /// <param name="context">The roots, home and environment of the refresh.</param>
    /// <returns>The servers. Empty when this source has none.</returns>
    IReadOnlyList<McpServerSpec> Servers(McpLocatorContext context);
}
