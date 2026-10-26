namespace OmpOS.Presentation.Kanban;

/// <summary>The state of the whole Kanban board, which decides what the Kanban tab shows.</summary>
public enum BoardState
{
    /// <summary>The sessions are being read. The tab shows skeleton rows and no spinner.</summary>
    Loading,

    /// <summary>No session exists. The tab shows the empty copy and a way to start one.</summary>
    Empty,

    /// <summary>Reading the sessions failed and there is nothing to show. The tab shows the reason and a Retry button.</summary>
    Error,

    /// <summary>At least one session exists. The tab shows the columns.</summary>
    Ready,
}
