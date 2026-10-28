namespace Autonogrammer.Presentation.Kanban;

/// <summary>The source of the Substrate fields of a card: task, graph, lessons and lease holder.</summary>
public interface IKanbanCardDetailsSource
{
    /// <summary>Reads what Substrate knows about one session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The details, or null when Substrate reports nothing for the session or cannot be reached.</returns>
    Task<KanbanCardDetails?> LoadAsync(string sessionId, CancellationToken cancellationToken);
}
