namespace OmpOS.Presentation.Mcp;

/// <summary>A configuration file that exists.</summary>
/// <param name="Origin">The file to show.</param>
/// <param name="Scope">Project or user.</param>
/// <param name="ServerCount">How many usable server entries the file holds.</param>
/// <param name="Problem">One sentence when the whole file could not be used; otherwise <see langword="null"/>.</param>
public sealed record McpFileEntry(string Origin, McpEntryScope Scope, int ServerCount, string? Problem);
