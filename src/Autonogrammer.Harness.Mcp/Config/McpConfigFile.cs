namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>A configuration file that exists, and what was read from it.</summary>
/// <param name="Origin">The file to show: relative to the workspace root, or <c>~/</c> relative.</param>
/// <param name="Scope">Project or user.</param>
/// <param name="Format">The file's dialect.</param>
/// <param name="ServerCount">How many usable server entries the file holds, before precedence removes any.</param>
/// <param name="Problem">One plain sentence when the whole file could not be used (invalid JSON, oversize, unreadable); otherwise <see langword="null"/>.</param>
public sealed record McpConfigFile(string Origin, McpScope Scope, McpConfigFormat Format, int ServerCount, string? Problem);
