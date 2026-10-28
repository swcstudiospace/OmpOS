using OmpOS.Harness.Mcp.Config;

namespace OmpOS.Harness.Mcp.Catalog;

/// <summary>The catalog as of one refresh or change.</summary>
/// <param name="Entries">Every effective server in precedence order.</param>
/// <param name="Files">Every configuration file that exists.</param>
/// <param name="Diagnostics">Every configuration problem, including problems with the state file.</param>
/// <param name="OwnedConfigOrigin">The file the add-from-template action writes, to show; <see langword="null"/> when no workspace root is open.</param>
public sealed record McpCatalogSnapshot(
    IReadOnlyList<McpCatalogEntry> Entries,
    IReadOnlyList<McpConfigFile> Files,
    IReadOnlyList<McpDiagnostic> Diagnostics,
    string? OwnedConfigOrigin)
{
    /// <summary>Gets the snapshot before the first refresh.</summary>
    public static McpCatalogSnapshot Empty { get; } = new([], [], [], null);
}
