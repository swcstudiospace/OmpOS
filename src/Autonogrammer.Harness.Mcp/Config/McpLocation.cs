namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>One place a configuration file may be.</summary>
/// <param name="Scope">Project or user.</param>
/// <param name="Format">The file's dialect.</param>
/// <param name="Path">The absolute path in the file store's path form.</param>
/// <param name="Origin">The path to show: relative to the workspace root, or <c>~/</c> relative for a user file.</param>
/// <param name="WorkspaceRoot">The workspace root a project file belongs to, or <see langword="null"/> for a user file.</param>
/// <param name="Priority">The position in precedence order; lower wins.</param>
public sealed record McpLocation(
    McpScope Scope,
    McpConfigFormat Format,
    string Path,
    string Origin,
    string? WorkspaceRoot,
    int Priority);
