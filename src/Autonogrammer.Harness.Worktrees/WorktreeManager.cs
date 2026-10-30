namespace Autonogrammer.Worktrees;

/// <summary>
/// Gives each agent run its own git worktree under an operator-controlled root directory.
/// </summary>
/// <remarks>
/// A run's worktree lives at <c>&lt;root&gt;/&lt;runId&gt;</c> on branch <c>autonogrammer/run/&lt;runId&gt;</c>.
/// Creation and removal are serialized per repository, so concurrent runs in one repository never share a
/// path or a branch. No command run by this class changes the main working tree of a repository.
/// </remarks>
public sealed class WorktreeManager
{
    /// <summary>
    /// The branch namespace for run branches. The full branch name is this prefix followed by the run id.
    /// </summary>
    public const string BranchPrefix = "autonogrammer/run/";

    private readonly string _root;
    private readonly string _git;
    private readonly KeyedAsyncLock _repositoryLocks = new();
    private readonly Lock _reservationGate = new();
    private readonly HashSet<string> _reservedRunIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a manager that places run worktrees under <paramref name="rootDirectory"/>.
    /// </summary>
    /// <param name="rootDirectory">The operator-controlled directory that holds run worktrees. It is created on first use.</param>
    /// <param name="gitExecutable">The git executable path, or a name resolved via PATH.</param>
    public WorktreeManager(string rootDirectory, string gitExecutable = "git")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(gitExecutable);

        _root = Path.GetFullPath(rootDirectory);
        _git = gitExecutable;
    }

    /// <summary>
    /// Gets the absolute operator-controlled root directory.
    /// </summary>
    public string RootDirectory => _root;

    /// <summary>
    /// Creates the worktree and branch for a run.
    /// </summary>
    /// <param name="repositoryPath">A path inside the repository (the main checkout or one of its worktrees).</param>
    /// <param name="runId">The run id: 1 to 64 ASCII letters, digits or hyphens.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created run.</returns>
    /// <exception cref="InvalidRunIdException">The run id is invalid. Nothing is created.</exception>
    /// <exception cref="RunAlreadyExistsException">A worktree path or branch for the run id already exists, or another create for it is in progress.</exception>
    /// <exception cref="DirectoryNotFoundException">The repository path does not exist.</exception>
    /// <exception cref="WorktreeGitException">git could not be started or failed.</exception>
    public async Task<WorktreeRun> CreateAsync(
        string repositoryPath,
        string runId,
        CancellationToken cancellationToken = default)
    {
        WorktreeRunId.Validate(runId, nameof(runId));
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        if (!Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException($"Repository directory '{repositoryPath}' does not exist.");
        }

        var runPath = Path.Combine(_root, runId);
        var branch = BranchPrefix + runId;

        ReserveRunId(runId);
        try
        {
            var commonDir = await ResolveCommonDirAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
            using var releaser = await _repositoryLocks.AcquireAsync(commonDir, cancellationToken).ConfigureAwait(false);

            if (Directory.Exists(runPath) || File.Exists(runPath))
            {
                throw new RunAlreadyExistsException($"Run '{runId}' already has a worktree directory.", runId);
            }

            if (await BranchExistsAsync(commonDir, branch, cancellationToken).ConfigureAwait(false))
            {
                throw new RunAlreadyExistsException($"Run '{runId}' already has a branch '{branch}'.", runId);
            }

            Directory.CreateDirectory(_root);

            var added = await GitProcess.RunAsync(
                _git,
                repositoryPath,
                ["worktree", "add", "-b", branch, runPath],
                cancellationToken).ConfigureAwait(false);

            if (added.ExitCode != 0)
            {
                await RollbackCreateAsync(commonDir, runPath, branch).ConfigureAwait(false);
                throw new WorktreeGitException(
                    $"git worktree add failed for run '{runId}' with exit code {added.ExitCode}: {added.StandardError.Trim()}",
                    added.ExitCode,
                    added.StandardError);
            }

            return new WorktreeRun(runId, runPath, branch);
        }
        finally
        {
            ReleaseRunId(runId);
        }
    }

    /// <summary>
    /// Removes a run's worktree and deletes its branch.
    /// </summary>
    /// <param name="runId">The run id to close.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="InvalidRunIdException">The run id is invalid.</exception>
    /// <exception cref="RunNotFoundException">No worktree directory exists under the root for the run id.</exception>
    /// <exception cref="WorktreeGitException">git could not be started or failed.</exception>
    public async Task CloseAsync(string runId, CancellationToken cancellationToken = default)
    {
        WorktreeRunId.Validate(runId, nameof(runId));

        var runPath = Path.Combine(_root, runId);
        var branch = BranchPrefix + runId;

        if (!Directory.Exists(runPath))
        {
            throw new RunNotFoundException($"Run '{runId}' has no worktree under the operator root.", runId);
        }

        var commonDir = await ResolveCommonDirAsync(runPath, cancellationToken).ConfigureAwait(false);
        using var releaser = await _repositoryLocks.AcquireAsync(commonDir, cancellationToken).ConfigureAwait(false);

        await GitProcess.RunOrThrowAsync(
            _git,
            commonDir,
            $"worktree remove '{runPath}'",
            [GitDirArgument(commonDir), "worktree", "remove", "--force", runPath],
            cancellationToken).ConfigureAwait(false);

        if (await BranchExistsAsync(commonDir, branch, cancellationToken).ConfigureAwait(false))
        {
            await GitProcess.RunOrThrowAsync(
                _git,
                commonDir,
                $"branch -D '{branch}'",
                [GitDirArgument(commonDir), "branch", "-D", branch],
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Lists the run worktrees of a repository: worktrees whose branch is in the <c>autonogrammer/run/</c> namespace
    /// and whose directory name equals the run id.
    /// </summary>
    /// <param name="repositoryPath">A path inside the repository.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The active runs, in the order git lists them.</returns>
    public async Task<IReadOnlyList<WorktreeRun>> ListAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        var output = await GitProcess.RunOrThrowAsync(
            _git,
            repositoryPath,
            "worktree list",
            ["worktree", "list", "--porcelain"],
            cancellationToken).ConfigureAwait(false);

        return ParseRuns(output);
    }

    internal static IReadOnlyList<WorktreeRun> ParseRuns(string porcelain)
    {
        var runs = new List<WorktreeRun>();
        string? path = null;
        string? branch = null;

        foreach (var raw in porcelain.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                AddRunIfOwned(runs, path, branch);
                path = null;
                branch = null;
            }
            else if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                path = line["worktree ".Length..];
            }
            else if (line.StartsWith("branch refs/heads/", StringComparison.Ordinal))
            {
                branch = line["branch refs/heads/".Length..];
            }
        }

        AddRunIfOwned(runs, path, branch);
        return runs;
    }

    private static void AddRunIfOwned(List<WorktreeRun> runs, string? path, string? branch)
    {
        if (path is null || branch is null || !branch.StartsWith(BranchPrefix, StringComparison.Ordinal))
        {
            return;
        }

        var runId = branch[BranchPrefix.Length..];
        if (!WorktreeRunId.IsValid(runId) || !string.Equals(Path.GetFileName(path), runId, StringComparison.Ordinal))
        {
            return;
        }

        runs.Add(new WorktreeRun(runId, path, branch));
    }

    private void ReserveRunId(string runId)
    {
        lock (_reservationGate)
        {
            if (!_reservedRunIds.Add(runId))
            {
                throw new RunAlreadyExistsException($"Run '{runId}' is already being created.", runId);
            }
        }
    }

    private void ReleaseRunId(string runId)
    {
        lock (_reservationGate)
        {
            _reservedRunIds.Remove(runId);
        }
    }

    private async Task<string> ResolveCommonDirAsync(string path, CancellationToken cancellationToken)
    {
        var output = await GitProcess.RunOrThrowAsync(
            _git,
            path,
            $"rev-parse --git-common-dir in '{path}'",
            ["rev-parse", "--path-format=absolute", "--git-common-dir"],
            cancellationToken).ConfigureAwait(false);

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(output.Trim()));
    }

    private async Task<bool> BranchExistsAsync(string commonDir, string branch, CancellationToken cancellationToken)
    {
        var result = await GitProcess.RunAsync(
            _git,
            commonDir,
            [GitDirArgument(commonDir), "show-ref", "--verify", "--quiet", "refs/heads/" + branch],
            cancellationToken).ConfigureAwait(false);

        return result.ExitCode switch
        {
            0 => true,
            1 => false,
            _ => throw new WorktreeGitException(
                $"git show-ref failed for branch '{branch}' with exit code {result.ExitCode}: {result.StandardError.Trim()}",
                result.ExitCode,
                result.StandardError),
        };
    }

    private async Task RollbackCreateAsync(string commonDir, string runPath, string branch)
    {
        // The path and branch were absent under the repository lock and the run id is reserved,
        // so anything found here was created by this failed attempt.
        if (Directory.Exists(runPath))
        {
            Directory.Delete(runPath, recursive: true);
        }

        await GitProcess.RunOrThrowAsync(
            _git,
            commonDir,
            "worktree prune",
            [GitDirArgument(commonDir), "worktree", "prune"],
            CancellationToken.None).ConfigureAwait(false);

        if (await BranchExistsAsync(commonDir, branch, CancellationToken.None).ConfigureAwait(false))
        {
            await GitProcess.RunOrThrowAsync(
                _git,
                commonDir,
                $"branch -D '{branch}'",
                [GitDirArgument(commonDir), "branch", "-D", branch],
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static string GitDirArgument(string commonDir) => "--git-dir=" + commonDir;
}
