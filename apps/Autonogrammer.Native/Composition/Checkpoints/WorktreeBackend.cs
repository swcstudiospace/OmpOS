using Autonogrammer.Presentation.Worktrees;
using Autonogrammer.Worktrees;

namespace Autonogrammer.Native.Composition.Checkpoints;

/// <summary>
/// The desktop implementation of <see cref="IWorktreeBackend"/>: runs live under <c>&lt;state directory&gt;/worktrees</c> as git worktrees of the session's primary root,
/// are shown as patches and are applied back onto that root. The run id comes from the caller, and the worktree library validates it.
/// </summary>
internal sealed class WorktreeBackend : IWorktreeBackend
{
    private readonly WorktreeManager _manager;
    private readonly WorktreeApplier _applier;
    private readonly Func<string?> _primaryRoot;
    private readonly IWorktreeSetupRunner _setupRunner;

    /// <summary>Initializes a new instance of the <see cref="WorktreeBackend"/> class.</summary>
    /// <param name="stateDirectory">The head's state directory; runs live in its <c>worktrees</c> folder.</param>
    /// <param name="primaryRoot">Gives the repository the runs are made from, or <see langword="null"/> when no workspace is open.</param>
    /// <param name="setupRunner">Runs <c>.cursor/worktrees.json</c> in a new worktree, after the user's consent.</param>
    public WorktreeBackend(string stateDirectory, Func<string?> primaryRoot, IWorktreeSetupRunner setupRunner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectory);
        ArgumentNullException.ThrowIfNull(primaryRoot);
        ArgumentNullException.ThrowIfNull(setupRunner);
        _manager = new WorktreeManager(Path.Combine(stateDirectory, "worktrees"));
        _applier = new WorktreeApplier(_manager);
        _primaryRoot = primaryRoot;
        _setupRunner = setupRunner;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorktreeRunInfo>> ListAsync(CancellationToken cancellationToken)
    {
        var repository = ResolveRepository();
        var summaries = await _applier.ListAsync(repository, cancellationToken).ConfigureAwait(false);
        return [.. summaries.Select(static summary => new WorktreeRunInfo(summary.RunId, summary.Path, summary.Branch, summary.ChangedFiles, summary.Problem))];
    }

    /// <inheritdoc />
    public async Task<WorktreeStartResult> StartAsync(string runId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runId);
        var repository = ResolveRepository();
        var run = await _manager.CreateAsync(repository, runId, cancellationToken).ConfigureAwait(false);
        var setup = await _setupRunner.RunAsync(new WorktreeSetupRequest(run.Path, repository), cancellationToken).ConfigureAwait(false);
        return new WorktreeStartResult(new WorktreeRunInfo(run.RunId, run.Path, run.Branch, 0, null), setup.Message);
    }

    /// <inheritdoc />
    public async Task<WorktreePatchPreview> PreviewAsync(string runId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runId);
        var patch = await _applier.CreatePatchAsync(runId, ResolveRepository(), cancellationToken).ConfigureAwait(false);
        return new WorktreePatchPreview(
            patch.RunId,
            [.. patch.Files.Select(static file => new WorktreePatchFileInfo(file.Path, ToChange(file.Status), file.Added, file.Removed, file.IsBinary))],
            patch.Text,
            patch.IsTextTruncated);
    }

    /// <inheritdoc />
    public async Task<WorktreeApplyOutcome> ApplyAsync(string runId, WorktreeApplyMethod method, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runId);
        var mode = method switch
        {
            WorktreeApplyMethod.Clean => WorktreeApplyMode.Clean,
            WorktreeApplyMethod.ThreeWay => WorktreeApplyMode.ThreeWay,
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown apply method."),
        };
        var result = await _applier.ApplyAsync(runId, ResolveRepository(), mode, cancellationToken).ConfigureAwait(false);
        return new WorktreeApplyOutcome(ToState(result.Status), result.ConflictPaths, result.Message);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string runId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runId);
        return _applier.DeleteAsync(runId, cancellationToken);
    }

    private static WorktreeFileChange ToChange(WorktreeFileStatus status) => status switch
    {
        WorktreeFileStatus.Added => WorktreeFileChange.Added,
        WorktreeFileStatus.Modified => WorktreeFileChange.Modified,
        WorktreeFileStatus.Deleted => WorktreeFileChange.Deleted,
        _ => WorktreeFileChange.Other,
    };

    private static WorktreeApplyState ToState(WorktreeApplyStatus status) => status switch
    {
        WorktreeApplyStatus.Applied => WorktreeApplyState.Applied,
        WorktreeApplyStatus.Conflicts => WorktreeApplyState.Conflicts,
        WorktreeApplyStatus.NothingToApply => WorktreeApplyState.NothingToApply,
        WorktreeApplyStatus.Failed => WorktreeApplyState.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown apply status."),
    };

    private string ResolveRepository()
    {
        var root = _primaryRoot();
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("Open a workspace folder first: a worktree is made from the folder of the session.");
        }

        var full = Path.GetFullPath(root);
        if (!Directory.Exists(full))
        {
            throw new InvalidOperationException($"The workspace folder '{full}' does not exist, so no worktree can be made from it.");
        }

        for (var directory = new DirectoryInfo(full); directory is not null; directory = directory.Parent)
        {
            var marker = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(marker) || File.Exists(marker))
            {
                return full;
            }
        }

        throw new InvalidOperationException($"The workspace folder '{full}' is not inside a git repository, so no worktree can be made from it.");
    }
}
