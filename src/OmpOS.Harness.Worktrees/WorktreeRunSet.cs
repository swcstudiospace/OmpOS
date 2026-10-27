using System.Globalization;
using System.Text;

namespace OmpOS.Worktrees;

/// <summary>
/// Gives one agent run a worktree for every git root of a multi-root workspace. The first root is the primary: omp starts in its
/// worktree and every other worktree is an additional directory. A root that is not the top of a git repository is used in place and
/// reported as a warning. Each worktree is made by <see cref="WorktreeManager"/>, so it lives directly under the manager's root at
/// <c>&lt;runId&gt;-&lt;root name&gt;</c> on branch <c>ompos/run/&lt;runId&gt;-&lt;root name&gt;</c>, and two runs never share a branch.
/// </summary>
/// <remarks>
/// The set is created as one unit: if git fails for any root, the worktrees already made are removed and the result says which root
/// failed. The cap counts every run worktree under the manager's root, as Cursor's <c>cursor.worktreeMaxCount</c> does for its worktrees.
/// </remarks>
public sealed class WorktreeRunSet
{
    /// <summary>The default most worktrees allowed under one root: 25, the default of Cursor's <c>cursor.worktreeMaxCount</c>.</summary>
    public const int DefaultMaxWorktrees = 25;

    private const string FallbackSegment = "root";

    private readonly WorktreeManager _manager;
    private readonly int _maxWorktrees;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorktreeRunSet"/> class.
    /// </summary>
    /// <param name="manager">Creates and closes each worktree.</param>
    /// <param name="maxWorktrees">The most run worktrees allowed under the manager's root.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxWorktrees"/> is less than 1.</exception>
    public WorktreeRunSet(WorktreeManager manager, int maxWorktrees = DefaultMaxWorktrees)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWorktrees, 1);
        _manager = manager;
        _maxWorktrees = maxWorktrees;
    }

    /// <summary>Gets the most run worktrees allowed under the manager's root.</summary>
    public int MaxWorktrees => _maxWorktrees;

    /// <summary>
    /// Creates a worktree for every git root of a run and uses the other roots in place.
    /// </summary>
    /// <param name="roots">The roots, primary first. Names must be unique ignoring case; folders must exist and must not overlap.</param>
    /// <param name="runId">The run id: 1 to 64 ASCII letters, digits or hyphens. Each worktree id is the run id, a hyphen and the root name, and must fit in 64 characters.</param>
    /// <param name="cancellationToken">Cancels the operation. Worktrees already made are removed.</param>
    /// <returns>One outcome per root. When a root failed, the result is not complete and every worktree made for the run was removed.</returns>
    /// <exception cref="InvalidRunIdException">The run id is invalid.</exception>
    /// <exception cref="ArgumentException">There is no root, a name is blank or repeated, or a path is not absolute.</exception>
    /// <exception cref="DirectoryNotFoundException">A root folder does not exist.</exception>
    /// <exception cref="NestedWorktreeRootException">Two roots are the same folder or one is inside another.</exception>
    /// <exception cref="WorktreeCapExceededException">The new worktrees would take the total above the cap. Nothing was created.</exception>
    public async Task<WorktreeRunSetResult> CreateAsync(IReadOnlyList<WorktreeRootSpec> roots, string runId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        WorktreeRunId.Validate(runId, nameof(runId));
        var folders = Normalize(roots);
        RejectOverlap(folders);

        var gitRoots = folders.Count(folder => IsGitRoot(folder.Path));
        var existing = CountExistingWorktrees();
        if (existing + gitRoots > _maxWorktrees)
        {
            throw new WorktreeCapExceededException(_maxWorktrees, existing, gitRoots);
        }

        var ids = WorktreeIds(runId, folders);
        var outcomes = new WorktreeRootOutcome[folders.Count];
        var created = new List<int>();
        var failedAt = -1;
        try
        {
            for (var index = 0; index < folders.Count; index++)
            {
                var folder = folders[index];
                if (!IsGitRoot(folder.Path))
                {
                    outcomes[index] = new WorktreeRootOutcome(
                        folder.Name,
                        folder.Path,
                        WorktreeRootStatus.UsedInPlace,
                        folder.Path,
                        null,
                        $"'{folder.Name}' is not the top of a git repository, so the run uses the folder in place: its changes are not isolated in a worktree.");
                    continue;
                }

                try
                {
                    var run = await _manager.CreateAsync(folder.Path, ids[index], cancellationToken).ConfigureAwait(false);
                    outcomes[index] = new WorktreeRootOutcome(folder.Name, folder.Path, WorktreeRootStatus.Created, run.Path, run, null);
                    created.Add(index);
                }
                catch (Exception exception) when (exception is WorktreeGitException or RunAlreadyExistsException)
                {
                    outcomes[index] = new WorktreeRootOutcome(folder.Name, folder.Path, WorktreeRootStatus.Failed, folder.Path, null, $"'{folder.Name}': {exception.Message}");
                    failedAt = index;
                    break;
                }
            }
        }
        catch
        {
            // Cancellation or an unexpected failure: no caller gets the result, so no worktree may be left behind.
            await RollBackAsync(created, outcomes, folders).ConfigureAwait(false);
            throw;
        }

        if (failedAt >= 0)
        {
            await RollBackAsync(created, outcomes, folders).ConfigureAwait(false);
            for (var index = failedAt + 1; index < folders.Count; index++)
            {
                outcomes[index] = new WorktreeRootOutcome(
                    folders[index].Name,
                    folders[index].Path,
                    WorktreeRootStatus.NotAttempted,
                    folders[index].Path,
                    null,
                    $"'{folders[index].Name}' was not attempted because '{folders[failedAt].Name}' failed.");
            }
        }

        return new WorktreeRunSetResult(runId, outcomes);
    }

    /// <summary>
    /// Removes every worktree and branch a run set created. A worktree that is already gone is not an error. Every worktree is
    /// attempted even when one fails.
    /// </summary>
    /// <param name="result">The result of <see cref="CreateAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="AggregateException">One or more worktrees could not be removed; the inner exceptions say which.</exception>
    public async Task CloseAsync(WorktreeRunSetResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        var failures = new List<Exception>();
        foreach (var outcome in result.Roots.Where(root => root.Status == WorktreeRootStatus.Created))
        {
            try
            {
                await _manager.CloseAsync(outcome.Run!.RunId, cancellationToken).ConfigureAwait(false);
            }
            catch (RunNotFoundException)
            {
                // Already closed, for example by an earlier call: closing is idempotent.
            }
            catch (WorktreeGitException exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("Some worktrees of the run set could not be removed.", failures);
        }
    }

    /// <summary>
    /// Returns the worktree run ids of a set: <c>&lt;runId&gt;-&lt;root name&gt;</c> with the name reduced to ASCII letters, digits and
    /// hyphens, made unique, and cut so that the id has at most 64 characters.
    /// </summary>
    /// <param name="runId">The run id of the set.</param>
    /// <param name="roots">The roots, in order.</param>
    /// <returns>One id per root.</returns>
    internal static string[] WorktreeIds(string runId, IReadOnlyList<WorktreeRootSpec> roots)
    {
        var available = WorktreeRunId.MaxLength - runId.Length - 1;
        if (available < 1)
        {
            throw new ArgumentException($"The run id '{runId}' is too long to leave room for a root name in a worktree id of {WorktreeRunId.MaxLength} characters.", nameof(runId));
        }

        var ids = new string[roots.Count];
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < roots.Count; index++)
        {
            var segment = Segment(roots[index].Name);
            var candidate = Fit(runId, segment, suffix: null, available);
            for (var number = 2; !taken.Add(candidate); number++)
            {
                candidate = Fit(runId, segment, "-" + number.ToString(CultureInfo.InvariantCulture), available);
            }

            ids[index] = candidate;
        }

        return ids;
    }

    private static string Fit(string runId, string segment, string? suffix, int available)
    {
        var room = available - (suffix?.Length ?? 0);
        var cut = segment.Length > room ? segment[..room].TrimEnd('-') : segment;
        return runId + "-" + (cut.Length == 0 ? FallbackSegment[..Math.Min(FallbackSegment.Length, room)] : cut) + suffix;
    }

    private static string Segment(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            var allowed = character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');
            if (allowed)
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var segment = builder.ToString().TrimEnd('-');
        return segment.Length == 0 ? FallbackSegment : segment;
    }

    private static List<WorktreeRootSpec> Normalize(IReadOnlyList<WorktreeRootSpec> roots)
    {
        if (roots.Count == 0)
        {
            throw new ArgumentException("A run set needs at least one root.", nameof(roots));
        }

        var folders = new List<WorktreeRootSpec>(roots.Count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            ArgumentNullException.ThrowIfNull(root);
            ArgumentException.ThrowIfNullOrWhiteSpace(root.Name, nameof(roots));
            ArgumentException.ThrowIfNullOrWhiteSpace(root.Path, nameof(roots));
            if (!Path.IsPathRooted(root.Path))
            {
                throw new ArgumentException($"The path of root '{root.Name}' must be absolute.", nameof(roots));
            }

            if (!names.Add(root.Name.Trim()))
            {
                throw new ArgumentException($"Two roots are named '{root.Name}'.", nameof(roots));
            }

            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root.Path));
            if (!Directory.Exists(full))
            {
                throw new DirectoryNotFoundException($"The folder of root '{root.Name}' does not exist: '{full}'.");
            }

            folders.Add(new WorktreeRootSpec(root.Name.Trim(), full));
        }

        return folders;
    }

    private static void RejectOverlap(List<WorktreeRootSpec> folders)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (var later = 1; later < folders.Count; later++)
        {
            for (var earlier = 0; earlier < later; earlier++)
            {
                var a = folders[earlier];
                var b = folders[later];
                if (string.Equals(a.Path, b.Path, comparison))
                {
                    throw new NestedWorktreeRootException($"Roots '{a.Name}' and '{b.Name}' are the same folder.");
                }

                if (IsInside(a.Path, b.Path, comparison))
                {
                    throw new NestedWorktreeRootException($"Root '{b.Name}' is inside root '{a.Name}'. Roots must not overlap.");
                }

                if (IsInside(b.Path, a.Path, comparison))
                {
                    throw new NestedWorktreeRootException($"Root '{a.Name}' is inside root '{b.Name}'. Roots must not overlap.");
                }
            }
        }
    }

    private static bool IsInside(string parent, string child, StringComparison comparison)
    {
        var prefix = parent.EndsWith(Path.DirectorySeparatorChar) ? parent : parent + Path.DirectorySeparatorChar;
        return child.StartsWith(prefix, comparison) && child.Length > prefix.Length;
    }

    private static bool IsGitRoot(string path)
    {
        var marker = Path.Combine(path, ".git");
        return Directory.Exists(marker) || File.Exists(marker);
    }

    private int CountExistingWorktrees() =>
        Directory.Exists(_manager.RootDirectory) ? Directory.EnumerateDirectories(_manager.RootDirectory).Count() : 0;

    private async Task RollBackAsync(List<int> created, WorktreeRootOutcome[] outcomes, List<WorktreeRootSpec> folders)
    {
        foreach (var index in created)
        {
            var run = outcomes[index].Run!;
            try
            {
                await _manager.CloseAsync(run.RunId, CancellationToken.None).ConfigureAwait(false);
                outcomes[index] = new WorktreeRootOutcome(
                    folders[index].Name,
                    folders[index].Path,
                    WorktreeRootStatus.RolledBack,
                    folders[index].Path,
                    null,
                    $"'{folders[index].Name}': its worktree was removed because another root failed.");
            }
            catch (Exception exception) when (exception is WorktreeGitException or RunNotFoundException)
            {
                outcomes[index] = outcomes[index] with
                {
                    Detail = $"'{folders[index].Name}': its worktree could not be removed after another root failed ({exception.Message}); remove '{run.Path}' by hand.",
                };
            }
        }
    }
}
