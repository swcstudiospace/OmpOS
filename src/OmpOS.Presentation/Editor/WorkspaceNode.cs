using System.Collections.ObjectModel;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Editor;

/// <summary>
/// One row of the workspace file tree. A directory's children are empty until it is first expanded, and then they are listed
/// once and kept, so collapsing and expanding again does not list the directory again.
/// </summary>
public sealed class WorkspaceNode : ObservableModel
{
    private readonly ObservableCollection<WorkspaceNode> _children = [];
    private bool _isExpanded;

    internal WorkspaceNode(WorkspaceTree owner, WorkspaceEntry entry, WorkspaceNode? parent)
    {
        Owner = owner;
        Entry = entry;
        Parent = parent;
        Depth = parent is null ? 0 : parent.Depth + 1;
        Children = new ReadOnlyObservableCollection<WorkspaceNode>(_children);
    }

    /// <summary>Gets the tree that owns the node.</summary>
    internal WorkspaceTree Owner { get; }

    /// <summary>Gets the entry the node shows.</summary>
    public WorkspaceEntry Entry { get; }

    /// <summary>Gets the node's name.</summary>
    public string Name => Entry.Name;

    /// <summary>Gets the normalised workspace-relative path of the node.</summary>
    public string RelativePath => Entry.RelativePath;

    /// <summary>Gets whether the node is a directory.</summary>
    public bool IsDirectory => Entry.IsDirectory;

    /// <summary>Gets the parent directory, or null for a top-level node.</summary>
    public WorkspaceNode? Parent { get; }

    /// <summary>Gets the nesting depth. Top-level nodes are at depth zero.</summary>
    public int Depth { get; }

    /// <summary>Gets the children. They are empty until the directory is expanded for the first time.</summary>
    public ReadOnlyObservableCollection<WorkspaceNode> Children { get; }

    /// <summary>Gets whether the directory's children have been listed.</summary>
    public bool IsLoaded { get; internal set; }

    /// <summary>Gets whether a directory is expanded. A file is never expanded.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        internal set => SetProperty(ref _isExpanded, value);
    }

    internal void SetChildren(IEnumerable<WorkspaceNode> children)
    {
        _children.Clear();
        foreach (var child in children)
        {
            _children.Add(child);
        }

        IsLoaded = true;
    }
}
