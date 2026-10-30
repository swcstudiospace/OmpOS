namespace Autonogrammer.UI.Components.Shell;

/// <summary>The kind of address the workbench shell shows.</summary>
public enum WorkbenchViewKind
{
    /// <summary>The Agents view, <c>/</c>: the selected agent's thread.</summary>
    Agents = 0,

    /// <summary>The Kanban view, <c>/kanban</c>.</summary>
    Kanban = 1,

    /// <summary>The Agents view with one session chosen by the address, <c>/sessions/{id}</c>.</summary>
    Session = 2,

    /// <summary>The Settings dialog, <c>/settings</c> or <c>/settings/{page}</c>, over the view that was showing.</summary>
    Settings = 3,

    /// <summary>The About view, <c>/about</c>.</summary>
    About = 4,
}
