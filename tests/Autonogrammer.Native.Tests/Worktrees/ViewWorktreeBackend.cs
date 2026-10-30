using Autonogrammer.Presentation.Worktrees;

namespace Autonogrammer.Native.Tests.Worktrees;

/// <summary>A worktree backend that keeps its runs in memory and records what it was asked, for the view tests.</summary>
internal sealed class ViewWorktreeBackend : IWorktreeBackend
{
    /// <summary>Gets the runs that exist.</summary>
    public List<WorktreeRunInfo> Runs { get; } = [];

    /// <summary>Gets or sets the outcome <see cref="ApplyAsync"/> returns.</summary>
    public WorktreeApplyOutcome Outcome { get; set; } = new(WorktreeApplyState.Applied, [], "Applied.");

    /// <summary>Gets the calls made, as text.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Adds a run with a branch named after it.</summary>
    /// <param name="runId">The run id.</param>
    /// <param name="changedFiles">The number of files it changed.</param>
    public void AddRun(string runId, int changedFiles) => Runs.Add(new WorktreeRunInfo(runId, "/wt/" + runId, "autonogrammer/run/" + runId, changedFiles, null));

    /// <inheritdoc/>
    public Task<IReadOnlyList<WorktreeRunInfo>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WorktreeRunInfo>>([.. Runs]);

    /// <inheritdoc/>
    public Task<WorktreeStartResult> StartAsync(string runId, CancellationToken cancellationToken)
    {
        Calls.Add("start " + runId);
        var run = new WorktreeRunInfo(runId, "/wt/" + runId, "autonogrammer/run/" + runId, 0, null);
        Runs.Add(run);
        return Task.FromResult(new WorktreeStartResult(run, string.Empty));
    }

    /// <inheritdoc/>
    public Task<WorktreePatchPreview> PreviewAsync(string runId, CancellationToken cancellationToken)
    {
        Calls.Add("preview " + runId);
        return Task.FromResult(new WorktreePatchPreview(
            runId,
            [
                new WorktreePatchFileInfo("src/a.txt", WorktreeFileChange.Modified, 3, 1, false),
                new WorktreePatchFileInfo("logo.png", WorktreeFileChange.Added, 0, 0, true),
            ],
            "diff",
            false));
    }

    /// <inheritdoc/>
    public Task<WorktreeApplyOutcome> ApplyAsync(string runId, WorktreeApplyMethod method, CancellationToken cancellationToken)
    {
        Calls.Add("apply " + runId + " " + method);
        return Task.FromResult(Outcome);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string runId, CancellationToken cancellationToken)
    {
        Calls.Add("delete " + runId);
        Runs.RemoveAll(run => string.Equals(run.RunId, runId, StringComparison.Ordinal));
        return Task.CompletedTask;
    }
}
