using System.Collections.ObjectModel;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// The workspace file tree (ADE-10): the open folder, its expandable directories, and a keyboard model over the visible rows.
/// </summary>
/// <remarks>
/// Opening a folder sets the root and lists it. A directory's children are listed on its first expansion and kept. Every
/// listed entry must be a plain name directly under its parent, so a file system that reports a path outside the directory
/// is refused before any node is created.
/// </remarks>
public sealed class WorkspaceTree : ObservableModel
{
    private readonly ObservableCollection<WorkspaceNode> _roots = [];
    private IWorkspaceFileSystem? _fileSystem;
    private IAsyncWorkspaceFileSystem? _asyncFileSystem;
    private string? _rootPath;
    private WorkspaceNode? _selected;

    /// <summary>Creates a tree with no folder open.</summary>
    public WorkspaceTree()
    {
        Nodes = new ReadOnlyObservableCollection<WorkspaceNode>(_roots);
    }

    /// <summary>Gets the top-level rows: the children of the workspace root.</summary>
    public ReadOnlyObservableCollection<WorkspaceNode> Nodes { get; }

    /// <summary>Gets the absolute path of the open workspace root, or null when no folder is open.</summary>
    public string? RootPath
    {
        get => _rootPath;
        private set => SetProperty(ref _rootPath, value);
    }

    /// <summary>Gets the selected row, or null when the tree has no rows.</summary>
    public WorkspaceNode? Selected
    {
        get => _selected;
        private set => SetProperty(ref _selected, value);
    }

    /// <summary>Opens a folder as the workspace root and lists its children. The first row is selected.</summary>
    /// <param name="fileSystem">The file system that lists the folder.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystem"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The file system returned an entry that is not directly under the root. The tree is unchanged.</exception>
    public void OpenFolder(IWorkspaceFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        var roots = LoadChildren(fileSystem, string.Empty, parent: null);
        _fileSystem = fileSystem;
        _asyncFileSystem = null;
        RootPath = fileSystem.RootPath;
        _roots.Clear();
        foreach (var root in roots)
        {
            _roots.Add(root);
        }

        Selected = VisibleRows().FirstOrDefault();
    }

    /// <summary>Expands a directory, listing its children on the first expansion.</summary>
    /// <param name="node">A directory row of this tree.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> is a file or does not belong to this tree.</exception>
    /// <exception cref="InvalidOperationException">No folder is open.</exception>
    public void Expand(WorkspaceNode node)
    {
        RequireDirectoryRow(node);

        if (!node.IsLoaded)
        {
            node.SetChildren(LoadChildren(RequireFileSystem(), node.RelativePath, node));
        }

        node.IsExpanded = true;
    }

    /// <summary>Collapses a directory. When the selected row was inside it, the directory becomes the selection.</summary>
    /// <param name="node">A directory row of this tree.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> is a file or does not belong to this tree.</exception>
    public void Collapse(WorkspaceNode node)
    {
        RequireDirectoryRow(node);

        node.IsExpanded = false;
        if (Selected is not null && IsInside(Selected, node))
        {
            Selected = node;
        }
    }

    /// <summary>Selects a visible row of this tree.</summary>
    /// <param name="node">The row to select.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> does not belong to this tree, or is hidden inside a collapsed directory.</exception>
    public void Select(WorkspaceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!VisibleRows().Contains(node))
        {
            throw new ArgumentException("Only a visible row of this tree can be selected.", nameof(node));
        }

        Selected = node;
    }

    /// <summary>
    /// Expands each directory along a workspace-relative path, loading each level as it goes, and returns the last directory.
    /// The path is normalised and checked before anything is listed, so an escaping path lists nothing.
    /// </summary>
    /// <param name="relativeDirectory">The workspace-relative path of a directory. It must not be empty.</param>
    /// <returns>The directory's row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="relativeDirectory"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The path escapes the root, is empty, or names no directory. Nothing is listed.
    /// </exception>
    /// <exception cref="InvalidOperationException">No folder is open.</exception>
    public WorkspaceNode ExpandPath(string relativeDirectory)
    {
        var path = WorkspacePath.Normalize(relativeDirectory);
        if (path.Length == 0)
        {
            throw new ArgumentException("Name a directory inside the workspace. The root is always expanded.", nameof(relativeDirectory));
        }

        var segments = path.Split('/');
        var node = FindDirectory(_roots, segments[0], relativeDirectory);
        Expand(node);
        for (var index = 1; index < segments.Length; index++)
        {
            node = FindDirectory(node.Children, segments[index], relativeDirectory);
            Expand(node);
        }

        return node;
    }

    /// <summary>
    /// Opens a folder whose files are listed without blocking (a remote machine) as the workspace root and lists its children. The first row is
    /// selected. Use <see cref="ExpandAsync"/>, <see cref="ExpandPathAsync"/> and <see cref="HandleKeyAsync"/> for the directories of such a tree.
    /// </summary>
    /// <param name="fileSystem">The file system that lists the folder.</param>
    /// <param name="cancellationToken">Cancels the listing. The tree is unchanged then.</param>
    /// <returns>A task that completes when the folder is open.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystem"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The file system returned an entry that is not directly under the root. The tree is unchanged.</exception>
    public async Task OpenFolderAsync(IAsyncWorkspaceFileSystem fileSystem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        var entries = await fileSystem.ListDirectoryAsync(string.Empty, cancellationToken).ConfigureAwait(true);
        var roots = BuildChildren(entries, string.Empty, parent: null);
        _fileSystem = null;
        _asyncFileSystem = fileSystem;
        RootPath = fileSystem.RootPath;
        _roots.Clear();
        foreach (var root in roots)
        {
            _roots.Add(root);
        }

        Selected = VisibleRows().FirstOrDefault();
    }

    /// <summary>Expands a directory of a tree opened with <see cref="OpenFolderAsync"/>, listing its children on the first expansion.</summary>
    /// <param name="node">A directory row of this tree.</param>
    /// <param name="cancellationToken">Cancels the listing. The directory stays collapsed then.</param>
    /// <returns>A task that completes when the directory is expanded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> is a file or does not belong to this tree.</exception>
    /// <exception cref="InvalidOperationException">No folder is open.</exception>
    public async Task ExpandAsync(WorkspaceNode node, CancellationToken cancellationToken)
    {
        RequireDirectoryRow(node);

        if (!node.IsLoaded)
        {
            var entries = await RequireAsyncFileSystem().ListDirectoryAsync(node.RelativePath, cancellationToken).ConfigureAwait(true);
            node.SetChildren(BuildChildren(entries, node.RelativePath, node));
        }

        node.IsExpanded = true;
    }

    /// <summary>The asynchronous form of <see cref="ExpandPath"/>: expands each directory along a workspace-relative path and returns the last one.</summary>
    /// <param name="relativeDirectory">The workspace-relative path of a directory. It must not be empty.</param>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The directory's row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="relativeDirectory"/> is null.</exception>
    /// <exception cref="ArgumentException">The path escapes the root, is empty, or names no directory. Nothing is listed for an escaping path.</exception>
    /// <exception cref="InvalidOperationException">No folder is open.</exception>
    public async Task<WorkspaceNode> ExpandPathAsync(string relativeDirectory, CancellationToken cancellationToken)
    {
        var path = WorkspacePath.Normalize(relativeDirectory);
        if (path.Length == 0)
        {
            throw new ArgumentException("Name a directory inside the workspace. The root is always expanded.", nameof(relativeDirectory));
        }

        var segments = path.Split('/');
        var node = FindDirectory(_roots, segments[0], relativeDirectory);
        await ExpandAsync(node, cancellationToken).ConfigureAwait(true);
        for (var index = 1; index < segments.Length; index++)
        {
            node = FindDirectory(node.Children, segments[index], relativeDirectory);
            await ExpandAsync(node, cancellationToken).ConfigureAwait(true);
        }

        return node;
    }

    /// <summary>
    /// The asynchronous form of <see cref="HandleKey"/>: applies a key to the selection. A directory that has to be listed first (Right or Enter on
    /// a directory that was never expanded) is listed without blocking, and then the key is applied as <see cref="HandleKey"/> applies it.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The file to open when <paramref name="key"/> is <see cref="WorkspaceKey.Enter"/> on a file; otherwise null.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is not defined.</exception>
    public async Task<WorkspaceEntry?> HandleKeyAsync(WorkspaceKey key, CancellationToken cancellationToken)
    {
        if (Enum.IsDefined(key)
            && key is WorkspaceKey.Right or WorkspaceKey.Enter
            && _asyncFileSystem is not null
            && Selected is { IsDirectory: true, IsLoaded: false } node)
        {
            var entries = await _asyncFileSystem.ListDirectoryAsync(node.RelativePath, cancellationToken).ConfigureAwait(true);
            node.SetChildren(BuildChildren(entries, node.RelativePath, node));
        }

        return HandleKey(key);
    }

    /// <summary>Lists the visible rows in display order: each expanded directory's children follow it.</summary>
    /// <returns>The visible rows.</returns>
    public IReadOnlyList<WorkspaceNode> VisibleNodes() => VisibleRows().AsReadOnly();

    /// <summary>Applies a key to the selection, following the keyboard model.</summary>
    /// <param name="key">The key.</param>
    /// <returns>
    /// The file to open when <paramref name="key"/> is <see cref="WorkspaceKey.Enter"/> on a file; otherwise null. Enter on a directory
    /// toggles its expansion and returns null.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is not defined.</exception>
    /// <exception cref="InvalidOperationException">A directory cannot be listed.</exception>
    public WorkspaceEntry? HandleKey(WorkspaceKey key)
    {
        if (!Enum.IsDefined(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key));
        }

        var rows = VisibleRows();
        if (rows.Count == 0)
        {
            return null;
        }

        if (Selected is null)
        {
            Selected = rows[0];
            return null;
        }

        var node = Selected;
        switch (key)
        {
            case WorkspaceKey.Up:
                MoveTo(rows, rows.IndexOf(node) - 1);
                return null;
            case WorkspaceKey.Down:
                MoveTo(rows, rows.IndexOf(node) + 1);
                return null;
            case WorkspaceKey.Home:
                MoveTo(rows, 0);
                return null;
            case WorkspaceKey.End:
                MoveTo(rows, rows.Count - 1);
                return null;
            case WorkspaceKey.Right:
                MoveInto(node);
                return null;
            case WorkspaceKey.Left:
                MoveOut(node);
                return null;
            case WorkspaceKey.Enter:
                return Activate(node);
            default:
                throw new ArgumentOutOfRangeException(nameof(key));
        }
    }

    // Right expands a collapsed directory; on an expanded one it moves to the first child.
    private void MoveInto(WorkspaceNode node)
    {
        if (!node.IsDirectory)
        {
            return;
        }

        if (!node.IsExpanded)
        {
            Expand(node);
        }
        else if (node.Children.Count > 0)
        {
            Selected = node.Children[0];
        }
    }

    // Left collapses an expanded directory; otherwise it moves to the parent.
    private void MoveOut(WorkspaceNode node)
    {
        if (node.IsDirectory && node.IsExpanded)
        {
            Collapse(node);
        }
        else if (node.Parent is not null)
        {
            Selected = node.Parent;
        }
    }

    // Enter opens a file, and toggles a directory.
    private WorkspaceEntry? Activate(WorkspaceNode node)
    {
        if (!node.IsDirectory)
        {
            return node.Entry;
        }

        if (node.IsExpanded)
        {
            Collapse(node);
        }
        else
        {
            Expand(node);
        }

        return null;
    }

    private void MoveTo(List<WorkspaceNode> rows, int index)
    {
        Selected = rows[Math.Clamp(index, 0, rows.Count - 1)];
    }

    private List<WorkspaceNode> VisibleRows()
    {
        var rows = new List<WorkspaceNode>();
        AddVisible(_roots, rows);
        return rows;
    }

    private static void AddVisible(IEnumerable<WorkspaceNode> nodes, List<WorkspaceNode> rows)
    {
        foreach (var node in nodes)
        {
            rows.Add(node);
            if (node.IsExpanded)
            {
                AddVisible(node.Children, rows);
            }
        }
    }

    private static bool IsInside(WorkspaceNode node, WorkspaceNode directory)
    {
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, directory))
            {
                return true;
            }
        }

        return false;
    }

    private static WorkspaceNode FindDirectory(IEnumerable<WorkspaceNode> level, string name, string requested)
    {
        return level.FirstOrDefault(node => node.IsDirectory && string.Equals(node.Name, name, StringComparison.Ordinal))
            ?? throw new ArgumentException($"No directory named '{name}' lies along '{requested}'.", nameof(requested));
    }

    private List<WorkspaceNode> LoadChildren(IWorkspaceFileSystem fileSystem, string parentPath, WorkspaceNode? parent)
    {
        return BuildChildren(fileSystem.ListDirectory(parentPath), parentPath, parent);
    }

    private List<WorkspaceNode> BuildChildren(IReadOnlyList<WorkspaceEntry> entries, string parentPath, WorkspaceNode? parent)
    {
        foreach (var entry in entries)
        {
            if (!WorkspacePath.IsPlainName(entry.Name)
                || !string.Equals(entry.RelativePath, WorkspacePath.Combine(parentPath, entry.Name), StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The file system listed an entry outside the directory '{parentPath}'.");
            }
        }

        return entries
            .OrderBy(entry => entry.IsDirectory ? 0 : 1)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new WorkspaceNode(this, entry, parent))
            .ToList();
    }

    private IWorkspaceFileSystem RequireFileSystem()
    {
        return _fileSystem ?? throw new InvalidOperationException(
            _asyncFileSystem is null ? "No folder is open." : "This folder was opened asynchronously: expand its directories with ExpandAsync.");
    }

    private IAsyncWorkspaceFileSystem RequireAsyncFileSystem()
    {
        return _asyncFileSystem ?? throw new InvalidOperationException(
            _fileSystem is null ? "No folder is open." : "This folder was opened synchronously: expand its directories with Expand.");
    }

    private void RequireDirectoryRow(WorkspaceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!node.IsDirectory)
        {
            throw new ArgumentException("Only a directory can be expanded or collapsed.", nameof(node));
        }

        if (!ReferenceEquals(node.Owner, this) || !IsAttached(node))
        {
            throw new ArgumentException("The row does not belong to this tree.", nameof(node));
        }
    }

    // A row is attached when its top-level ancestor is still one of the open folder's rows; rows from an earlier folder are not.
    private bool IsAttached(WorkspaceNode node)
    {
        var top = node;
        while (top.Parent is not null)
        {
            top = top.Parent;
        }

        return _roots.Contains(top);
    }
}
