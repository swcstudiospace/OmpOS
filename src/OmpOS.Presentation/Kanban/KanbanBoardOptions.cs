namespace OmpOS.Presentation.Kanban;

/// <summary>The head hooks of <see cref="KanbanBoardModel"/>.</summary>
public sealed class KanbanBoardOptions
{
    /// <summary>
    /// Gets the lookup of the active model and provider of a chat by session identifier, or null when the head reports none. It is called
    /// on the context that owns the models.
    /// </summary>
    public Func<string, KanbanModelInfo?>? ModelOf { get; init; }

    /// <summary>
    /// Gets the source of the Substrate fields of the cards, or null when the head has none; every card then shows those fields as unavailable.
    /// </summary>
    public IKanbanCardDetailsSource? Details { get; init; }

    /// <summary>Gets the steering service the cards send their messages through, or null when the head has none; the cards then cannot steer.</summary>
    public ICardSteering? Steering { get; init; }

    /// <summary>
    /// Gets whether the board starts in <see cref="BoardState.Loading"/>, for a head that is still restoring its sessions. The head ends it
    /// with <see cref="KanbanBoardModel.CompleteLoading"/>.
    /// </summary>
    public bool StartLoading { get; init; }
}
