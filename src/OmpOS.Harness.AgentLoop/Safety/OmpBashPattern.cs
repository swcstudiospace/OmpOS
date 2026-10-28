namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>One entry of omp's <c>bash.patterns</c> setting: a wildcard over the whole command line and what omp does with a command that matches.</summary>
/// <param name="Match">The wildcard; <c>*</c> matches any text.</param>
/// <param name="Approval">The policy: <c>deny</c>, <c>prompt</c> or <c>allow</c>.</param>
public sealed record OmpBashPattern(string Match, string Approval);
