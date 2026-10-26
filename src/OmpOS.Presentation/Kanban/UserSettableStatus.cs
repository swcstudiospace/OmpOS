namespace OmpOS.Presentation.Kanban;

/// <summary>
/// The only statuses a user may set on a card by dragging it. Every other column shows a <see cref="RuntimeStatus"/>, which only a runtime
/// signal sets (VX-04). A card the user placed here stays here until its runtime status changes.
/// </summary>
public enum UserSettableStatus
{
    /// <summary>Parked work that is not being run: the <c>Backlog</c> column.</summary>
    Backlog,

    /// <summary>Work the user put away: the <c>Archived</c> column.</summary>
    Archived,
}
