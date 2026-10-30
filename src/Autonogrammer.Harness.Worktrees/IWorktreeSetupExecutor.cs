namespace Autonogrammer.Worktrees;

/// <summary>Runs a worktree's setup commands. It does not ask for consent: <see cref="IWorktreeSetupRunner"/> does that before it calls an executor.</summary>
public interface IWorktreeSetupExecutor
{
    /// <summary>Runs the commands or the script.</summary>
    /// <param name="execution">What to run and where.</param>
    /// <param name="cancellationToken">Cancels the run; the running command is stopped.</param>
    /// <returns>How it ended.</returns>
    Task<WorktreeSetupResult> ExecuteAsync(WorktreeSetupExecution execution, CancellationToken cancellationToken);
}
