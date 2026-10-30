namespace Autonogrammer.Workspace.Sets;

/// <summary>
/// The ordered folders one session covers. The first root is the primary: it is the working directory of omp, and every other
/// root is passed to omp as an additional directory. A set is immutable; every change returns a new set. A set can hold a
/// combination <see cref="WorkspaceSetValidator"/> rejects (duplicates, nesting, missing folders), so a picker can show the
/// user's input and the reasons it is not usable yet.
/// </summary>
public sealed class WorkspaceSet
{
    private readonly WorkspaceSetRoot[] _roots;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceSet"/> class.
    /// </summary>
    /// <param name="id">The identifier of the set, used by the recent-set store as its key.</param>
    /// <param name="name">The name shown to the user.</param>
    /// <param name="roots">
    /// The roots in order. The root flagged <see cref="WorkspaceSetRoot.IsPrimary"/> moves to the front; when none is flagged the first is
    /// the primary. The stored roots carry the normalised flag.
    /// </param>
    /// <exception cref="ArgumentException">There is no root, a root is null, or more than one root is flagged primary.</exception>
    public WorkspaceSet(string id, string name, IEnumerable<WorkspaceSetRoot> roots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(roots);

        var given = roots.ToArray();
        if (given.Length == 0)
        {
            throw new ArgumentException("A workspace set needs at least one root.", nameof(roots));
        }

        if (Array.Exists(given, root => root is null))
        {
            throw new ArgumentException("A workspace set cannot hold a null root.", nameof(roots));
        }

        var flagged = given.Count(root => root.IsPrimary);
        if (flagged > 1)
        {
            throw new ArgumentException("A workspace set has exactly one primary root.", nameof(roots));
        }

        var primaryIndex = flagged == 0 ? 0 : Array.FindIndex(given, root => root.IsPrimary);
        _roots = new WorkspaceSetRoot[given.Length];
        _roots[0] = given[primaryIndex] with { IsPrimary = true };
        var next = 1;
        for (var index = 0; index < given.Length; index++)
        {
            if (index != primaryIndex)
            {
                _roots[next++] = given[index] with { IsPrimary = false };
            }
        }

        Id = id;
        Name = name;
    }

    /// <summary>Gets the identifier of the set.</summary>
    public string Id { get; }

    /// <summary>Gets the name shown to the user.</summary>
    public string Name { get; }

    /// <summary>Gets the roots in order; the first is the primary.</summary>
    public IReadOnlyList<WorkspaceSetRoot> Roots => _roots;

    /// <summary>Gets the primary root, the working directory of omp.</summary>
    public WorkspaceSetRoot Primary => _roots[0];

    /// <summary>Gets the path of the primary root.</summary>
    public string PrimaryPath => _roots[0].Path;

    /// <summary>Gets the environment that owns the primary root, or null for this machine. A valid set has one environment for all roots.</summary>
    public string? EnvironmentId => _roots[0].EnvironmentId;

    /// <summary>
    /// Gets the paths of every root except the primary, in order. This is the list omp receives as <c>--add-dir</c> arguments and what
    /// <c>OmpLaunchPlanner.Plan</c> takes as its additional directories.
    /// </summary>
    public IReadOnlyList<string> AdditionalDirectoryPaths => _roots.Skip(1).Select(root => root.Path).ToArray();

    /// <summary>
    /// Builds a set from folder paths. The first path is the primary. Display names are the last path segments, made unique by a
    /// numeric suffix, and identifiers are their lowercase forms.
    /// </summary>
    /// <param name="name">The name of the set. Its lowercase form becomes the set identifier.</param>
    /// <param name="paths">The folder paths, at least one.</param>
    /// <param name="environmentId">The environment that owns every folder, or null for this machine.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentException">There is no path.</exception>
    public static WorkspaceSet FromPaths(string name, IEnumerable<string> paths, string? environmentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(paths);

        var roots = new List<WorkspaceSetRoot>();
        foreach (var path in paths)
        {
            roots.Add(NewRoot(roots, path, displayName: null, environmentId, isPrimary: roots.Count == 0));
        }

        return new WorkspaceSet(WorkspaceRootNaming.Slug(name), name, roots);
    }

    /// <summary>Returns a set with one more root at the end.</summary>
    /// <param name="path">The folder path. It is stored as given; validation judges it.</param>
    /// <param name="displayName">The display name, or null for the last path segment, made unique among the roots.</param>
    /// <returns>The new set. The environment of the new root is the environment of the set.</returns>
    public WorkspaceSet AddFolder(string path, string? displayName = null) =>
        new(Id, Name, [.. _roots, NewRoot(_roots, path, displayName, EnvironmentId, isPrimary: false)]);

    /// <summary>
    /// Returns a set without a root. Removing the primary makes the next root the primary.
    /// </summary>
    /// <param name="rootId">The identifier of the root.</param>
    /// <returns>The new set; this set when no root has the identifier.</returns>
    /// <exception cref="InvalidOperationException">The root is the only one: a set keeps at least one.</exception>
    public WorkspaceSet Remove(string rootId)
    {
        ArgumentNullException.ThrowIfNull(rootId);
        var index = Array.FindIndex(_roots, root => string.Equals(root.Id, rootId, StringComparison.Ordinal));
        if (index < 0)
        {
            return this;
        }

        if (_roots.Length == 1)
        {
            throw new InvalidOperationException("A workspace set keeps at least one root.");
        }

        var kept = _roots.Where((_, position) => position != index).ToList();
        return new WorkspaceSet(Id, Name, kept);
    }

    /// <summary>Returns a set whose primary is another root. The other roots keep their order.</summary>
    /// <param name="rootId">The identifier of the new primary.</param>
    /// <returns>The new set; this set when no root has the identifier.</returns>
    public WorkspaceSet WithPrimary(string rootId)
    {
        ArgumentNullException.ThrowIfNull(rootId);
        if (!Array.Exists(_roots, root => string.Equals(root.Id, rootId, StringComparison.Ordinal)))
        {
            return this;
        }

        return new WorkspaceSet(
            Id,
            Name,
            _roots.Select(root => root with { IsPrimary = string.Equals(root.Id, rootId, StringComparison.Ordinal) }));
    }

    /// <summary>Returns a set with another name. The identifier is unchanged.</summary>
    /// <param name="name">The new name.</param>
    public WorkspaceSet Rename(string name) => new(Id, name, _roots);

    private static WorkspaceSetRoot NewRoot(IReadOnlyCollection<WorkspaceSetRoot> existing, string path, string? displayName, string? environmentId, bool isPrimary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var names = new HashSet<string>(existing.Select(root => root.DisplayName), StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(existing.Select(root => root.Id), StringComparer.Ordinal);
        var name = displayName is { Length: > 0 }
            ? displayName
            : WorkspaceRootNaming.MakeUnique(WorkspaceRootNaming.DefaultName(path), names, "-");
        var id = WorkspaceRootNaming.MakeUnique(WorkspaceRootNaming.Slug(name), ids, "-");
        return new WorkspaceSetRoot(id, path, name, environmentId, isPrimary);
    }
}
