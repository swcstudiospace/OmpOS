namespace OmpOS.Presentation.Worktrees;

/// <summary>A new run worktree and what its setup did.</summary>
/// <param name="Run">The worktree.</param>
/// <param name="SetupMessage">One sentence about the <c>.cursor/worktrees.json</c> setup (ran, not approved, none), or an empty string when the host does not run setups.</param>
public sealed record WorktreeStartResult(WorktreeRunInfo Run, string SetupMessage);
