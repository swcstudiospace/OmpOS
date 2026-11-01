namespace Autonogrammer.Harness.PluginBundles;

/// <summary>The <c>.mcp.json</c> document a bundle may carry.</summary>
internal sealed class BundleMcpFileDocument
{
    /// <summary>Gets or sets the servers, keyed by the name inside the bundle.</summary>
    public Dictionary<string, BundleMcpServerDocument> McpServers { get; set; } = new(StringComparer.Ordinal);
}
