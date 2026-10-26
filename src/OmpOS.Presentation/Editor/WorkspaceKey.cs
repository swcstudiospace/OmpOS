namespace OmpOS.Presentation.Editor;

/// <summary>The keys the file tree responds to. A head maps its own key events onto these.</summary>
public enum WorkspaceKey
{
    /// <summary>Selects the previous visible node. Stops at the first.</summary>
    Up,

    /// <summary>Selects the next visible node. Stops at the last.</summary>
    Down,

    /// <summary>Expands a collapsed directory; on an expanded directory, selects its first child.</summary>
    Right,

    /// <summary>Collapses an expanded directory; otherwise selects the parent directory.</summary>
    Left,

    /// <summary>Opens a file, or toggles a directory's expansion.</summary>
    Enter,

    /// <summary>Selects the first visible node.</summary>
    Home,

    /// <summary>Selects the last visible node.</summary>
    End,
}
