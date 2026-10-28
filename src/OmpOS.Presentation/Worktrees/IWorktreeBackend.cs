namespace OmpOS.Presentation.Worktrees;

/// <summary>
/// The worktrees of the open repository. The desktop head implements it over <c>WorktreeManager</c>, <c>WorktreeApplier</c> and <c>IWorktreeSetupRunner</c> of <c>OmpOS.Harness.Worktrees</c>;
/// presentation never references that library. A failure the user can act on is thrown as an exception whose message is shown as it is (a cap reached, a repository that is not git).
/// </summary>
public interface IWorktreeBackend
{
    /// <summary>Lists the run worktrees with the number of files each changed.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The runs.</returns>
    Task<IReadOnlyList<WorktreeRunInfo>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Creates a worktree and a branch for a run and runs its setup behind the setup consent.</summary>
    /// <param name="runId">The run id.</param>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>The worktree and the sentence about its setup.</returns>
    Task<WorktreeStartResult> StartAsync(string runId, CancellationToken cancellationToken);

    /// <summary>Makes a run's changes a patch for review.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The patch.</returns>
    Task<WorktreePatchPreview> PreviewAsync(string runId, CancellationToken cancellationToken);

    /// <summary>Applies a run's changes to the open checkout, uncommitted.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="method">What to do when the patch does not fit.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How it ended.</returns>
    Task<WorktreeApplyOutcome> ApplyAsync(string runId, WorktreeApplyMethod method, CancellationToken cancellationToken);

    /// <summary>Removes a run's worktree and deletes its branch.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the run is gone.</returns>
    Task DeleteAsync(string runId, CancellationToken cancellationToken);
}
