using System.Collections.ObjectModel;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Editor;

/// <summary>
/// The file explorer of a multi-root workspace: one <see cref="WorkspaceRootTree"/> per folder, the root name as the first level,
/// and below it the ordinary <see cref="WorkspaceTree"/> of that folder. A root that cannot be opened shows its error and leaves the
/// others working. Paths shown to the user and put in prompts are root-qualified: <c>rootName/relative/path</c>.
/// </summary>
public sealed class MultiRootWorkspaceTree : ObservableModel
{
    private readonly ObservableCollection<WorkspaceRootTree> _roots = [];
    private WorkspaceRootTree? _selectedRoot;

    /// <summary>Creates an explorer with no folder open.</summary>
    public MultiRootWorkspaceTree()
    {
        Roots = new ReadOnlyObservableCollection<WorkspaceRootTree>(_roots);
    }

    /// <summary>Gets the roots in order; the primary comes first when the caller listed it first.</summary>
    public ReadOnlyObservableCollection<WorkspaceRootTree> Roots { get; }

    /// <summary>Gets the root the user last selected, or null when no root is open or none was selected.</summary>
    public WorkspaceRootTree? SelectedRoot
    {
        get => _selectedRoot;
        private set => SetProperty(ref _selectedRoot, value);
    }

    /// <summary>Gets the roots that failed to open.</summary>
    public IReadOnlyList<WorkspaceRootTree> FailedRoots => _roots.Where(root => root.HasError).ToArray();

    /// <summary>
    /// Opens every root. Each root is opened on its own: a failure is recorded on that root and the rest still open. The previous roots are replaced.
    /// </summary>
    /// <param name="sources">The roots, in display order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> or one of its entries is null.</exception>
    /// <exception cref="ArgumentException">Two roots share an identifier or a name (ignoring case), or a name is blank. Nothing is changed.</exception>
    public void Open(IReadOnlyList<WorkspaceRootSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentException.ThrowIfNullOrWhiteSpace(source.Id, nameof(sources));
            ArgumentException.ThrowIfNullOrWhiteSpace(source.DisplayName, nameof(sources));
            ArgumentNullException.ThrowIfNull(source.Open);
            if (!ids.Add(source.Id) || !names.Add(source.DisplayName.Trim()))
            {
                throw new ArgumentException($"The root '{source.DisplayName}' repeats an identifier or a name.", nameof(sources));
            }
        }

        var opened = sources.Select(source => new WorkspaceRootTree(source)).ToList();
        foreach (var root in opened)
        {
            root.Open();
        }

        _roots.Clear();
        foreach (var root in opened)
        {
            _roots.Add(root);
        }

        SelectedRoot = opened.FirstOrDefault();
        NotifyPropertyChanged(nameof(FailedRoots));
    }

    /// <summary>
    /// Opens one root again, for example after its folder reappeared. Other roots are untouched. When the root fails again it keeps its previous tree and records the error.
    /// </summary>
    /// <param name="rootId">The identifier of the root.</param>
    /// <exception cref="ArgumentException">No root has the identifier.</exception>
    public void Refresh(string rootId)
    {
        var root = RequireRoot(rootId);
        root.Open();
        NotifyPropertyChanged(nameof(FailedRoots));
    }

    /// <summary>Selects a root.</summary>
    /// <param name="rootId">The identifier of the root.</param>
    /// <exception cref="ArgumentException">No root has the identifier.</exception>
    public void SelectRoot(string rootId) => SelectedRoot = RequireRoot(rootId);

    /// <summary>Returns a workspace-relative path with its root name in front.</summary>
    /// <param name="root">A root of this explorer.</param>
    /// <param name="relativePath">The normalised workspace-relative path within the root; empty names the root itself.</param>
    /// <returns><c>name/relative</c>, or just the name for the root itself.</returns>
    /// <exception cref="ArgumentException">The root does not belong to this explorer.</exception>
    public string Qualify(WorkspaceRootTree root, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(relativePath);
        if (!_roots.Contains(root))
        {
            throw new ArgumentException("The root does not belong to this explorer.", nameof(root));
        }

        var relative = WorkspacePath.Normalize(relativePath);
        return relative.Length == 0 ? root.DisplayName : root.DisplayName + "/" + relative;
    }

    /// <summary>Splits a root-qualified path into its root and the path inside it. The longest root name that is a whole leading segment run wins.</summary>
    /// <param name="qualifiedPath">A path of the form <c>name/relative</c> or just <c>name</c>.</param>
    /// <param name="root">The root named at the front, when found.</param>
    /// <param name="relativePath">The normalised path inside the root; empty for the root itself.</param>
    /// <returns>True when a root name leads the path and the rest is a path that stays inside the root.</returns>
    public bool TryResolve(string qualifiedPath, out WorkspaceRootTree? root, out string relativePath)
    {
        ArgumentNullException.ThrowIfNull(qualifiedPath);
        root = null;
        relativePath = string.Empty;
        var normalized = qualifiedPath.Replace('\\', '/').TrimStart('/');
        foreach (var candidate in _roots.OrderByDescending(item => item.DisplayName.Length))
        {
            var name = candidate.DisplayName;
            if (string.Equals(normalized, name, StringComparison.Ordinal))
            {
                root = candidate;
                return true;
            }

            if (!normalized.StartsWith(name + "/", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                relativePath = WorkspacePath.Normalize(normalized[(name.Length + 1)..]);
            }
            catch (ArgumentException)
            {
                return false;
            }

            root = candidate;
            return true;
        }

        return false;
    }

    private WorkspaceRootTree RequireRoot(string rootId)
    {
        ArgumentNullException.ThrowIfNull(rootId);
        return _roots.FirstOrDefault(root => string.Equals(root.Id, rootId, StringComparison.Ordinal))
            ?? throw new ArgumentException($"No root has the identifier '{rootId}'.", nameof(rootId));
    }
}
