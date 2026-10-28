namespace OmpOS.Presentation.Worktrees;

/// <summary>One candidate of a best-of-n run: the prompt and the worktree it runs in.</summary>
/// <param name="Prompt">The prompt every candidate gets.</param>
/// <param name="Index">The candidate's number, from 1.</param>
/// <param name="Count">How many candidates there are.</param>
/// <param name="Run">The candidate's worktree.</param>
public sealed record BestOfNRunRequest(string Prompt, int Index, int Count, WorktreeRunInfo Run);
