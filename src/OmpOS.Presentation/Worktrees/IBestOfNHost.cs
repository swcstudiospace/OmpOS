namespace OmpOS.Presentation.Worktrees;

/// <summary>
/// The head's side of <c>/best-of-n</c>: it starts an agent session in a worktree, sends it the prompt and waits for the turn to end. <see cref="WorktreeCommandsModel"/> creates the worktrees,
/// calls this once per candidate and compares the results; the real agent runs need a live session, so only the head can implement it.
/// </summary>
public interface IBestOfNHost
{
    /// <summary>Runs the prompt in one candidate's worktree.</summary>
    /// <param name="request">The prompt and the worktree.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>How the candidate ended. A run that cannot start is a failed result, not an exception.</returns>
    Task<BestOfNRunResult> RunAsync(BestOfNRunRequest request, CancellationToken cancellationToken);
}
