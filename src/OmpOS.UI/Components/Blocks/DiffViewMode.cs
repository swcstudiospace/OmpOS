namespace OmpOS.UI.Components.Blocks;

/// <summary>How <see cref="DiffView"/> lays a diff out.</summary>
public enum DiffViewMode
{
    /// <summary>One column: removed and added lines follow each other, as in a unified diff.</summary>
    Unified = 0,

    /// <summary>Two columns: the old text on the left, the new text on the right, with removed and added lines paired.</summary>
    SideBySide = 1,
}
