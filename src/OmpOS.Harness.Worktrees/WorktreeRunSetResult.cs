namespace OmpOS.Worktrees;

/// <summary>
/// The outcome of <see cref="WorktreeRunSet.CreateAsync"/>: one entry per root in the order they were given.
/// </summary>
public sealed class WorktreeRunSetResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorktreeRunSetResult"/> class.
    /// </summary>
    /// <param name="runId">The run id of the set.</param>
    /// <param name="roots">One outcome per root, primary first.</param>
    internal WorktreeRunSetResult(string runId, IReadOnlyList<WorktreeRootOutcome> roots)
    {
        RunId = runId;
        Roots = roots;
    }

    /// <summary>Gets the run id the set was created for.</summary>
    public string RunId { get; }

    /// <summary>Gets one outcome per root in the order the roots were given; the first is the primary.</summary>
    public IReadOnlyList<WorktreeRootOutcome> Roots { get; }

    /// <summary>Gets whether every root is usable: no root failed. A set that is not complete has been rolled back and must not be run.</summary>
    public bool IsComplete => !Roots.Any(root => root.Status == WorktreeRootStatus.Failed);

    /// <summary>Gets the directory the run starts omp in: the working path of the primary root.</summary>
    public string PrimaryWorkingPath => Roots[0].WorkingPath;

    /// <summary>Gets the working paths of every other root, in order. These are the <c>--add-dir</c> arguments of the run.</summary>
    public IReadOnlyList<string> AdditionalWorkingPaths => Roots.Skip(1).Select(root => root.WorkingPath).ToArray();

    /// <summary>Gets one warning per root that is used in place, naming it.</summary>
    public IReadOnlyList<string> Warnings =>
        Roots.Where(root => root.Status == WorktreeRootStatus.UsedInPlace).Select(root => root.Detail!).ToArray();
}
