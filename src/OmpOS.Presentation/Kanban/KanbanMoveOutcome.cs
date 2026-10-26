namespace OmpOS.Presentation.Kanban;

/// <summary>The result of dropping a card on a column with <see cref="KanbanBoardModel.Move"/>.</summary>
public enum KanbanMoveOutcome
{
    /// <summary>The card now sits in the user-settable column.</summary>
    Moved,

    /// <summary>The card already sits in that column; nothing changed.</summary>
    AlreadyThere,

    /// <summary>The column shows a runtime status, which only a runtime signal sets; the drop is refused and the card is unchanged.</summary>
    RefusedRuntimeColumn,

    /// <summary>No card has that identifier.</summary>
    UnknownCard,

    /// <summary>No column has that key.</summary>
    UnknownColumn,
}
