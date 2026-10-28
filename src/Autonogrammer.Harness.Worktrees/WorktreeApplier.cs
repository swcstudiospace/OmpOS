using System.Text;

namespace Autonogrammer.Worktrees;

/// <summary>
/// The worktree commands over <see cref="WorktreeManager"/>: list the run worktrees of a repository with the size of their changes, make a run's changes a reviewable patch, apply that patch
/// to a checkout with conflict reporting, and delete a run. Nothing here changes the checkout until <see cref="ApplyAsync"/> applies a patch, and it never commits.
/// </summary>
/// <remarks>
/// A patch is the difference between the run and the commit it started from (the common ancestor with the target checkout), so it holds committed and uncommitted changes and new files, and
/// ignored files are left out. Counting the changes in <see cref="ListAsync"/> changes nothing; building a patch stages the run's changes in the run's own worktree index, so review and apply a run while its agent is idle. Git itself writes the patch to a private temporary file
/// and applies it from there, so file content that is not UTF-8 is never decoded on the way.
/// </remarks>
public sealed class WorktreeApplier
{
    /// <summary>The most bytes of patch text kept in <see cref="WorktreePatch.Text"/>: 4 MiB.</summary>
    public const int MaxPatchTextBytes = 4 * 1024 * 1024;

    private readonly WorktreeManager _manager;
    private readonly string _git;

    /// <summary>Creates the commands over a manager.</summary>
    /// <param name="manager">Owns the run worktrees.</param>
    /// <param name="gitExecutable">The git executable path, or a name resolved via PATH.</param>
    public WorktreeApplier(WorktreeManager manager, string gitExecutable = "git")
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentException.ThrowIfNullOrWhiteSpace(gitExecutable);
        _manager = manager;
        _git = gitExecutable;
    }

    /// <summary>Lists the run worktrees of a repository with the number of files each changed.</summary>
    /// <param name="repositoryPath">A path inside the repository the runs were made from; it is the checkout the counts are measured against.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The runs, in the order git lists them. A run whose changes cannot be counted carries the reason.</returns>
    /// <exception cref="WorktreeGitException">git could not be started or failed to list the worktrees.</exception>
    public async Task<IReadOnlyList<WorktreeRunSummary>> ListAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        var summaries = new List<WorktreeRunSummary>();
        foreach (var run in await _manager.ListAsync(repositoryPath, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                var changed = await CountChangedFilesAsync(run.RunId, repositoryPath, cancellationToken).ConfigureAwait(false);
                summaries.Add(new WorktreeRunSummary(run.RunId, run.Path, run.Branch, changed, null));
            }
            catch (WorktreeGitException exception)
            {
                summaries.Add(new WorktreeRunSummary(run.RunId, run.Path, run.Branch, 0, exception.Message));
            }
            catch (RunNotFoundException exception)
            {
                summaries.Add(new WorktreeRunSummary(run.RunId, run.Path, run.Branch, 0, exception.Message));
            }
        }

        return summaries;
    }

    /// <summary>Makes a run's changes a patch for review.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="targetRepositoryPath">A path inside the checkout the patch is for.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The patch. It is empty when the run changed nothing.</returns>
    /// <exception cref="InvalidRunIdException">The run id is invalid.</exception>
    /// <exception cref="RunNotFoundException">The run has no worktree.</exception>
    /// <exception cref="WorktreeGitException">git could not be started or failed.</exception>
    public async Task<WorktreePatch> CreatePatchAsync(string runId, string targetRepositoryPath, CancellationToken cancellationToken = default)
    {
        var build = await BuildAsync(runId, targetRepositoryPath, cancellationToken).ConfigureAwait(false);
        try
        {
            var (text, truncated) = await ReadCappedAsync(build.PatchFile, cancellationToken).ConfigureAwait(false);
            return new WorktreePatch(runId, build.Branch, build.BaseCommit, build.Files, text, truncated);
        }
        finally
        {
            DeleteQuietly(build.PatchFile);
        }
    }

    /// <summary>Applies a run's patch to a checkout.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="targetRepositoryPath">A path inside the checkout the changes go to. They are left in its working tree, not committed.</param>
    /// <param name="mode">What to do when the patch does not fit.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How it ended. A patch that does not fit is a <see cref="WorktreeApplyStatus.Conflicts"/> result that lists the files, never an exception.</returns>
    /// <exception cref="InvalidRunIdException">The run id is invalid.</exception>
    /// <exception cref="RunNotFoundException">The run has no worktree.</exception>
    /// <exception cref="WorktreeGitException">git could not be started or failed while the patch was made.</exception>
    public async Task<WorktreeApplyResult> ApplyAsync(string runId, string targetRepositoryPath, WorktreeApplyMode mode, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        var build = await BuildAsync(runId, targetRepositoryPath, cancellationToken).ConfigureAwait(false);
        try
        {
            if (build.Files.Count == 0)
            {
                return new WorktreeApplyResult(WorktreeApplyStatus.NothingToApply, [], [], WorktreeCopy.NothingToApply(runId));
            }

            var top = await TopLevelAsync(targetRepositoryPath, cancellationToken).ConfigureAwait(false);
            var check = await GitProcess.RunAsync(_git, top, ["apply", "--check", "--binary", build.PatchFile], cancellationToken).ConfigureAwait(false);
            if (check.ExitCode == 0)
            {
                return await ApplyCleanAsync(runId, top, build, cancellationToken).ConfigureAwait(false);
            }

            var conflicts = WorktreePatchParser.ConflictPaths(check.StandardError, build.Files);
            return mode == WorktreeApplyMode.Clean
                ? new WorktreeApplyResult(WorktreeApplyStatus.Conflicts, build.Files, conflicts, WorktreeCopy.DoesNotFit(runId, conflicts))
                : await ApplyThreeWayAsync(runId, top, build, conflicts, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DeleteQuietly(build.PatchFile);
        }
    }

    /// <summary>Removes a run's worktree and deletes its branch. Changes that were not applied are lost.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the worktree and the branch are gone.</returns>
    /// <exception cref="InvalidRunIdException">The run id is invalid.</exception>
    /// <exception cref="RunNotFoundException">The run has no worktree.</exception>
    /// <exception cref="WorktreeGitException">git could not be started or failed.</exception>
    public Task DeleteAsync(string runId, CancellationToken cancellationToken = default) => _manager.CloseAsync(runId, cancellationToken);

    private async Task<WorktreeApplyResult> ApplyCleanAsync(string runId, string top, WorktreePatchBuild build, CancellationToken cancellationToken)
    {
        var applied = await GitProcess.RunAsync(_git, top, ["apply", "--binary", build.PatchFile], cancellationToken).ConfigureAwait(false);
        return applied.ExitCode == 0
            ? new WorktreeApplyResult(WorktreeApplyStatus.Applied, build.Files, [], WorktreeCopy.Applied(runId, build.Files.Count))
            : new WorktreeApplyResult(WorktreeApplyStatus.Failed, build.Files, [], WorktreeCopy.Failed(runId, applied.StandardError));
    }

    private async Task<WorktreeApplyResult> ApplyThreeWayAsync(string runId, string top, WorktreePatchBuild build, IReadOnlyList<string> reported, CancellationToken cancellationToken)
    {
        var merged = await GitProcess.RunAsync(_git, top, ["apply", "--3way", "--binary", build.PatchFile], cancellationToken).ConfigureAwait(false);
        if (merged.ExitCode == 0)
        {
            return new WorktreeApplyResult(WorktreeApplyStatus.Applied, build.Files, [], WorktreeCopy.AppliedThreeWay(runId, build.Files.Count));
        }

        var unmerged = await GitProcess.RunAsync(_git, top, ["diff", "--name-only", "--diff-filter=U", "--no-renames"], cancellationToken).ConfigureAwait(false);
        var known = build.Files.Select(static file => file.Path).ToHashSet(StringComparer.Ordinal);
        var conflicts = unmerged.StandardOutput.Split('\n')
            .Select(static line => line.TrimEnd('\r'))
            .Where(known.Contains)
            .Concat(WorktreePatchParser.ConflictPaths(merged.StandardError, build.Files))
            .Concat(reported)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return conflicts.Count == 0
            ? new WorktreeApplyResult(WorktreeApplyStatus.Failed, build.Files, [], WorktreeCopy.Failed(runId, merged.StandardError))
            : new WorktreeApplyResult(WorktreeApplyStatus.Conflicts, build.Files, conflicts, WorktreeCopy.ConflictMarkers(runId, conflicts));
    }

    private async Task<int> CountChangedFilesAsync(string runId, string repositoryPath, CancellationToken cancellationToken)
    {
        var worktree = Path.Combine(_manager.RootDirectory, runId);
        if (!Directory.Exists(worktree))
        {
            throw new RunNotFoundException($"Run '{runId}' has no worktree under the operator root.", runId);
        }

        var top = await TopLevelAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        var targetHead = (await GitProcess.RunOrThrowAsync(_git, top, "rev-parse HEAD", ["rev-parse", "HEAD"], cancellationToken).ConfigureAwait(false)).Trim();
        var baseCommit = (await GitProcess.RunOrThrowAsync(_git, worktree, "merge-base", ["merge-base", "HEAD", targetHead], cancellationToken).ConfigureAwait(false)).Trim();
        var tracked = await GitProcess.RunOrThrowAsync(_git, worktree, "diff --name-only", ["--no-optional-locks", "-c", "core.quotePath=false", "diff", "--name-only", "--no-renames", baseCommit], cancellationToken).ConfigureAwait(false);
        var untracked = await GitProcess.RunOrThrowAsync(_git, worktree, "ls-files --others", ["--no-optional-locks", "-c", "core.quotePath=false", "ls-files", "--others", "--exclude-standard"], cancellationToken).ConfigureAwait(false);
        return tracked.Split('\n').Concat(untracked.Split('\n')).Select(static line => line.TrimEnd('\r')).Where(static line => line.Length > 0).Distinct(StringComparer.Ordinal).Count();
    }

    private async Task<string> TopLevelAsync(string path, CancellationToken cancellationToken) =>
        (await GitProcess.RunOrThrowAsync(_git, path, "rev-parse --show-toplevel", ["rev-parse", "--show-toplevel"], cancellationToken).ConfigureAwait(false)).Trim();

    private async Task<WorktreePatchBuild> BuildAsync(string runId, string targetRepositoryPath, CancellationToken cancellationToken)
    {
        WorktreeRunId.Validate(runId, nameof(runId));
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRepositoryPath);

        var worktree = Path.Combine(_manager.RootDirectory, runId);
        if (!Directory.Exists(worktree))
        {
            throw new RunNotFoundException($"Run '{runId}' has no worktree under the operator root.", runId);
        }

        var top = await TopLevelAsync(targetRepositoryPath, cancellationToken).ConfigureAwait(false);
        var targetHead = (await GitProcess.RunOrThrowAsync(_git, top, "rev-parse HEAD", ["rev-parse", "HEAD"], cancellationToken).ConfigureAwait(false)).Trim();
        await GitProcess.RunOrThrowAsync(_git, worktree, "add -A", ["add", "-A"], cancellationToken).ConfigureAwait(false);
        var baseCommit = (await GitProcess.RunOrThrowAsync(_git, worktree, "merge-base", ["merge-base", "HEAD", targetHead], cancellationToken).ConfigureAwait(false)).Trim();
        var branch = (await GitProcess.RunOrThrowAsync(_git, worktree, "branch --show-current", ["branch", "--show-current"], cancellationToken).ConfigureAwait(false)).Trim();

        var numstat = await GitProcess.RunOrThrowAsync(_git, worktree, "diff --numstat", ["-c", "core.quotePath=false", "diff", "--cached", "--numstat", "--no-renames", baseCommit], cancellationToken).ConfigureAwait(false);
        var names = await GitProcess.RunOrThrowAsync(_git, worktree, "diff --name-status", ["-c", "core.quotePath=false", "diff", "--cached", "--name-status", "--no-renames", baseCommit], cancellationToken).ConfigureAwait(false);
        var files = WorktreePatchParser.ParseFiles(numstat, names);

        var patchFile = CreatePrivateFile();
        try
        {
            await GitProcess.RunOrThrowAsync(
                _git,
                worktree,
                "diff --binary",
                ["diff", "--cached", "--binary", "--full-index", "--no-renames", "--no-color", "--no-ext-diff", "--output=" + patchFile, baseCommit],
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            DeleteQuietly(patchFile);
            throw;
        }

        return new WorktreePatchBuild(patchFile, branch, baseCommit, files);
    }

    private static string CreatePrivateFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "autonogrammer-worktree-" + Guid.NewGuid().ToString("N") + ".patch");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using var stream = new FileStream(path, options);
        return path;
    }

    private static async Task<(string Text, bool Truncated)> ReadCappedAsync(string path, CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[(int)Math.Min(stream.Length, MaxPatchTextBytes)];
            await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
            return (Encoding.UTF8.GetString(buffer), stream.Length > MaxPatchTextBytes);
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A temporary patch file that cannot be deleted now is removed with the temporary folder.
        }
        catch (UnauthorizedAccessException)
        {
            // A temporary patch file that cannot be deleted now is removed with the temporary folder.
        }
    }
}
