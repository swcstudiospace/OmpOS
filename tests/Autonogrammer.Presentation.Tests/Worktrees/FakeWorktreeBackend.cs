using Autonogrammer.Presentation.Worktrees;

namespace Autonogrammer.Presentation.Tests.Worktrees;

/// <summary>A worktree backend that keeps its runs in memory and records what it was asked.</summary>
internal sealed class FakeWorktreeBackend : IWorktreeBackend
{
    /// <summary>Gets the runs that exist.</summary>
    public List<WorktreeRunInfo> Runs { get; } = [];

    /// <summary>Gets or sets the outcome <see cref="ApplyAsync"/> returns.</summary>
    public WorktreeApplyOutcome Outcome { get; set; } = new(WorktreeApplyState.Applied, [], "Applied.");

    /// <summary>Gets or sets the sentence about the setup that <see cref="StartAsync"/> returns.</summary>
    public string SetupMessage { get; set; } = string.Empty;

    /// <summary>Gets or sets an exception the next <see cref="StartAsync"/> throws, then clears.</summary>
    public Exception? StartFailure { get; set; }

    /// <summary>Gets or sets the run id whose deletion fails.</summary>
    public string? DeleteFailureFor { get; set; }

    /// <summary>Gets the calls made, as text.</summary>
    public List<string> Calls { get; } = [];

    /// <inheritdoc/>
    public Task<IReadOnlyList<WorktreeRunInfo>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WorktreeRunInfo>>([.. Runs]);

    /// <inheritdoc/>
    public Task<WorktreeStartResult> StartAsync(string runId, CancellationToken cancellationToken)
    {
        Calls.Add("start " + runId);
        if (StartFailure is not null)
        {
            var failure = StartFailure;
            StartFailure = null;
            return Task.FromException<WorktreeStartResult>(failure);
        }

        var run = new WorktreeRunInfo(runId, "/wt/" + runId, "autonogrammer/run/" + runId, 0, null);
        Runs.Add(run);
        return Task.FromResult(new WorktreeStartResult(run, SetupMessage));
    }

    /// <inheritdoc/>
    public Task<WorktreePatchPreview> PreviewAsync(string runId, CancellationToken cancellationToken)
    {
        Calls.Add("preview " + runId);
        return Task.FromResult(new WorktreePatchPreview(runId, [new WorktreePatchFileInfo("a.txt", WorktreeFileChange.Modified, 3, 1, false)], "diff", false));
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
        if (string.Equals(runId, DeleteFailureFor, StringComparison.Ordinal))
        {
            return Task.FromException(new IOException("locked"));
        }

        Runs.RemoveAll(run => string.Equals(run.RunId, runId, StringComparison.Ordinal));
        return Task.CompletedTask;
    }
}
