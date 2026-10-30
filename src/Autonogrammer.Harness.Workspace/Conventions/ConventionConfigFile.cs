namespace Autonogrammer.Workspace.Conventions;

/// <summary>A hooks or MCP configuration file that exists. Discovery lists it and does not parse it: the hooks and MCP phases do.</summary>
/// <param name="Scope">Project or user.</param>
/// <param name="SourceDirectory">The convention directory that holds the file, or an empty string for a file at the root itself (<c>.mcp.json</c>).</param>
/// <param name="Path">The file path in the source that listed it.</param>
/// <param name="Origin">The path to show: relative to the workspace root, or <c>~/</c> relative for a user file.</param>
public sealed record ConventionConfigFile(ConventionScope Scope, string SourceDirectory, string Path, string Origin);
