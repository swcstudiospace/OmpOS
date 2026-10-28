using OmpOS.Harness.Mcp.Config;

namespace OmpOS.Harness.Mcp.Catalog;

/// <summary>One server in the catalog with its decided state.</summary>
/// <param name="Server">The server as configured.</param>
/// <param name="Status">The state and its reason.</param>
/// <param name="IsEnabled">Whether the server is switched on: the user's choice, else the file's.</param>
/// <param name="IsOffered">Whether the server goes into the <c>mcpServers</c> array of the next session: enabled, complete and approved.</param>
/// <param name="IsConsentGranted">Whether the user approved the server's exact declaration. It is false for a server that needs no approval.</param>
public sealed record McpCatalogEntry(McpServerSpec Server, McpServerStatus Status, bool IsEnabled, bool IsOffered, bool IsConsentGranted);
