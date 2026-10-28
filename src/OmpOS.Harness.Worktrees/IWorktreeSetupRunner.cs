namespace OmpOS.Worktrees;

/// <summary>Runs the <c>.cursor/worktrees.json</c> setup of a new worktree, behind the setup consent: nothing runs until the user approved exactly this setup for this repository.</summary>
public interface IWorktreeSetupRunner
{
    /// <summary>Finds the setup for the current operating system, asks for consent and runs it.</summary>
    /// <param name="request">The worktree and the main checkout.</param>
    /// <param name="cancellationToken">Cancels the question or the run.</param>
    /// <returns>How it ended. A repository without a setup is <see cref="WorktreeSetupStatus.NoSetup"/>, not a failure.</returns>
    Task<WorktreeSetupResult> RunAsync(WorktreeSetupRequest request, CancellationToken cancellationToken);
}
