namespace Autonogrammer.Worktrees;

/// <summary>The result of a worktree's setup. An expected failure is a status, never an exception.</summary>
/// <param name="Status">How it ended.</param>
/// <param name="CommandsRun">How many commands ran to the end, or started and failed.</param>
/// <param name="Message">One sentence that says what happened.</param>
public sealed record WorktreeSetupResult(WorktreeSetupStatus Status, int CommandsRun, string Message);
