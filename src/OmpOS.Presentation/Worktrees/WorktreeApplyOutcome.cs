namespace OmpOS.Presentation.Worktrees;

/// <summary>The result of applying a run's patch.</summary>
/// <param name="State">How it ended.</param>
/// <param name="ConflictPaths">The files that conflict, or an empty list.</param>
/// <param name="Message">One sentence that says what happened.</param>
public sealed record WorktreeApplyOutcome(WorktreeApplyState State, IReadOnlyList<string> ConflictPaths, string Message);
