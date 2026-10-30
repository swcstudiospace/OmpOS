namespace Autonogrammer.Presentation.Settings.Connectors;

/// <summary>What one explicit check found, with no secret.</summary>
/// <param name="State">The state name, such as Connected or Failed.</param>
/// <param name="ToolNames">The tool names, empty when the check did not list any.</param>
public sealed record ConnectorCheckSnapshot(string State, IReadOnlyList<string> ToolNames);
