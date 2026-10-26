namespace OmpOS.Presentation.Kanban;

/// <summary>The head hooks of <see cref="KanbanBoardModel"/>.</summary>
public sealed class KanbanBoardOptions
{
    /// <summary>
    /// Gets the lookup of the active model and provider of a chat by session identifier, or null when the head reports none. It is called
    /// on the context that owns the models.
    /// </summary>
    public Func<string, KanbanModelInfo?>? ModelOf { get; init; }
}
