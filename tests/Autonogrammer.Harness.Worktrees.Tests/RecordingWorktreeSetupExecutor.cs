namespace Autonogrammer.Worktrees.Tests;

/// <summary>An executor that runs nothing and remembers what it was given.</summary>
internal sealed class RecordingWorktreeSetupExecutor : IWorktreeSetupExecutor
{
    /// <summary>Gets what it was asked to run.</summary>
    public List<WorktreeSetupExecution> Executions { get; } = [];

    /// <inheritdoc/>
    public Task<WorktreeSetupResult> ExecuteAsync(WorktreeSetupExecution execution, CancellationToken cancellationToken)
    {
        Executions.Add(execution);
        return Task.FromResult(new WorktreeSetupResult(WorktreeSetupStatus.Completed, execution.Setup.Commands.Count, "ran"));
    }
}
