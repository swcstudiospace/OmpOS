using Avalonia;
using OmpOS.Presentation.Editor;

namespace OmpOS.Native.Views.Explorer;

/// <summary>
/// One visible row of the workspace tree as the explorer draws it: the node, its indent by depth, and its label with an
/// expansion marker for directories. A row is a snapshot; the view builds new rows when the tree changes.
/// </summary>
internal sealed class WorkspaceRow
{
    private const double IndentPerLevel = 16;

    /// <summary>Creates the row for one visible node.</summary>
    /// <param name="node">The visible node.</param>
    public WorkspaceRow(WorkspaceNode node)
    {
        Node = node;
        Indent = new Thickness(node.Depth * IndentPerLevel, 0, 0, 0);
        Label = node.IsDirectory
            ? (node.IsExpanded ? $"\u25BE {node.Name}" : $"\u25B8 {node.Name}")
            : $"  {node.Name}";
    }

    /// <summary>Gets the node the row shows.</summary>
    public WorkspaceNode Node { get; }

    /// <summary>Gets the node's name, which is the row's accessible name.</summary>
    public string Name => Node.Name;

    /// <summary>Gets the left indent that shows the row's depth.</summary>
    public Thickness Indent { get; }

    /// <summary>Gets the text the row shows: the name, marked as expanded, collapsed, or a file.</summary>
    public string Label { get; }
}
