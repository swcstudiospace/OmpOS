using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.UI.Tests;

/// <summary>A real Kanban board over a fake session catalog. Only the sources are scripted: the catalog, the clock and the steering service.</summary>
internal sealed class KanbanBoardFixture : IDisposable
{
    private readonly SessionCatalogModel _catalog;

    /// <summary>Creates the board over the sessions in <paramref name="catalog"/>.</summary>
    /// <param name="catalog">The scripted catalog.</param>
    /// <param name="withSteering">Whether the board has a steering service.</param>
    /// <param name="startLoading">Whether the board starts in the loading state.</param>
    internal KanbanBoardFixture(WorkspaceSessionCatalog catalog, bool withSteering, bool startLoading = false)
    {
        Catalog = catalog;
        Steering = new WorkspaceCardSteering();
        _catalog = new SessionCatalogModel(catalog, new WorkspaceClock());
        Board = new KanbanBoardModel(
            _catalog,
            new WorkspaceClock(),
            new KanbanBoardOptions
            {
                Steering = withSteering ? Steering : null,
                StartLoading = startLoading,
            });
    }

    /// <summary>Gets the scripted catalog.</summary>
    internal WorkspaceSessionCatalog Catalog { get; }

    /// <summary>Gets the steering service the board was given (when it has one).</summary>
    internal WorkspaceCardSteering Steering { get; }

    /// <summary>Gets the board model under test.</summary>
    internal KanbanBoardModel Board { get; }

    /// <summary>Builds a session summary.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="title">The title.</param>
    /// <param name="lifecycle">The lifecycle, which the board maps to a runtime status.</param>
    /// <param name="kind">The session kind.</param>
    /// <returns>The summary.</returns>
    internal static SessionSummary Session(string id, string title, SessionLifecycle lifecycle, SessionKind kind = SessionKind.Chat) => new()
    {
        Id = id,
        Title = title,
        Lifecycle = lifecycle,
        CreatedAt = new DateTimeOffset(2026, 10, 27, 11, 0, 0, TimeSpan.Zero),
        LastActivity = new DateTimeOffset(2026, 10, 27, 11, 55, 0, TimeSpan.Zero),
        Kind = kind,
    };

    /// <summary>Stops the models.</summary>
    public void Dispose()
    {
        Board.Dispose();
        _catalog.Dispose();
    }
}
