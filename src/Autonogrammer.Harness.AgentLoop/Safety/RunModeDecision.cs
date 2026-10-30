namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>A decision with the audit record the Run mode page shows: the outcome, the step that settled it, in words why, and the rule that matched.</summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="Stage">The step that settled the call.</param>
/// <param name="Reason">One sentence saying why.</param>
/// <param name="Rule">The rule that matched, or null when none did.</param>
public sealed record RunModeDecision(RunModeOutcome Outcome, RunModeStage Stage, string Reason, PermissionRule? Rule = null);
