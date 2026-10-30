namespace Autonogrammer.Harness.Mcp.Catalog;

/// <summary>The state of a server with the one sentence that explains it and the tool count when it is known.</summary>
/// <param name="State">The state.</param>
/// <param name="Reason">Why the server is <see cref="McpServerState.Failed"/> or <see cref="McpServerState.Disabled"/>; <see langword="null"/> otherwise. It carries no secret.</param>
/// <param name="ToolCount">How many tools the server advertised, when a connection check or the registry knows; otherwise <see langword="null"/>.</param>
public sealed record McpServerStatus(McpServerState State, string? Reason = null, int? ToolCount = null);
