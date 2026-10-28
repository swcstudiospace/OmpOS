namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>One decision the engine made, for the Run mode page: what was asked, what was decided and which rule or step settled it.</summary>
/// <param name="CallId">The tool call's identifier.</param>
/// <param name="ToolName">The tool's display name.</param>
/// <param name="Kind">The kind of call.</param>
/// <param name="Subject">The command, file, address or MCP tool the call was about, cut to 200 characters.</param>
/// <param name="Decision">The decision and its audit record.</param>
/// <param name="At">When it was made.</param>
public sealed record RunModeAuditEntry(string CallId, string ToolName, RunModeToolKind Kind, string Subject, RunModeDecision Decision, DateTimeOffset At);
