using Autonogrammer.Contracts.Agents;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Views.Kanban;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Kanban;
using Avalonia.Controls;

namespace Autonogrammer.Native.Tests.Kanban;

/// <summary>
/// A real board over a fake session catalog, shown in a window as a <see cref="KanbanBoardView"/>. Only the sources are scripted: the catalog,
/// the clock, the steering service and the Substrate details.
/// </summary>
internal sealed class KanbanFixture : IDisposable
{
    private KanbanFixture(ManualTimeProvider clock, FakeSessionCatalog fake, SessionCatalogModel catalog, KanbanBoardModel board, Window window, KanbanBoardView view, FakeCardSteering steering, FakeCardDetails details)
    {
        Clock = clock;
        Fake = fake;
        Catalog = catalog;
        Board = board;
        Window = window;
        View = view;
        Steering = steering;
        Details = details;
    }

    /// <summary>Gets the clock.</summary>
    internal ManualTimeProvider Clock { get; }

    /// <summary>Gets the fake session catalog.</summary>
    internal FakeSessionCatalog Fake { get; }

    /// <summary>Gets the catalog model.</summary>
    internal SessionCatalogModel Catalog { get; }

    /// <summary>Gets the board model.</summary>
    internal KanbanBoardModel Board { get; }

    /// <summary>Gets the window that shows the view.</summary>
    internal Window Window { get; }

    /// <summary>Gets the board view.</summary>
    internal KanbanBoardView View { get; }

    /// <summary>Gets the scripted steering service.</summary>
    internal FakeCardSteering Steering { get; }

    /// <summary>Gets the scripted Substrate details.</summary>
    internal FakeCardDetails Details { get; }

    /// <summary>Builds a summary of a session that ended <paramref name="minutesAgo"/> minutes ago.</summary>
    /// <param name="clock">The clock.</param>
    /// <param name="id">The session identifier.</param>
    /// <param name="lifecycle">The lifecycle.</param>
    /// <param name="title">The title.</param>
    /// <param name="kind">The session kind.</param>
    /// <param name="minutesAgo">How long ago the last activity was.</param>
    /// <returns>The summary.</returns>
    internal static SessionSummary Session(ManualTimeProvider clock, string id, SessionLifecycle lifecycle, string title, SessionKind kind = SessionKind.Chat, int minutesAgo = 1)
    {
        var now = clock.GetUtcNow();
        return PanelFixtures.Summary(id, lifecycle, now.AddMinutes(-minutesAgo - 10), now.AddMinutes(-minutesAgo), title) with { Kind = kind };
    }

    /// <summary>Shows a board. Call it on the dispatcher thread of a headless session.</summary>
    /// <param name="populate">Adds the sessions. Null adds the default four: a running chat, a completed chat, a running terminal and a dropped terminal.</param>
    /// <param name="reducedMotion">Whether the window carries the reduced-motion class.</param>
    /// <param name="startLoading">Whether the board starts in the loading state.</param>
    /// <param name="withSteering">Whether the board has a steering service.</param>
    /// <returns>The fixture. The caller disposes it.</returns>
    internal static KanbanFixture Open(
        Action<FakeSessionCatalog, ManualTimeProvider>? populate = null,
        bool reducedMotion = false,
        bool startLoading = false,
        bool withSteering = true)
    {
        var clock = new ManualTimeProvider();
        var fake = new FakeSessionCatalog();
        if (populate is null)
        {
            fake.Sessions.Add(Session(clock, "run-1", SessionLifecycle.Running, "Build the board", minutesAgo: 2));
            fake.Sessions.Add(Session(clock, "done-1", SessionLifecycle.Completed, "Write the docs", minutesAgo: 30));
            fake.Sessions.Add(Session(clock, "term-1", SessionLifecycle.Running, "Shell session", SessionKind.Terminal, minutesAgo: 1));
            fake.Sessions.Add(Session(clock, "lost-1", SessionLifecycle.Lost, "Dropped terminal", SessionKind.Terminal, minutesAgo: 10));
        }
        else
        {
            populate(fake, clock);
        }

        var steering = new FakeCardSteering();
        var details = new FakeCardDetails();
        var catalog = new SessionCatalogModel(fake, clock);
        var board = new KanbanBoardModel(
            catalog,
            clock,
            new KanbanBoardOptions
            {
                ModelOf = id => string.Equals(id, "run-1", StringComparison.Ordinal) ? new KanbanModelInfo("gpt-5", "openai") : null,
                Details = details,
                Steering = withSteering ? steering : null,
                StartLoading = startLoading,
            });
        var view = new KanbanBoardView { DataContext = board };
        var window = ViewHarness.Show(view, reducedMotion, width: 1400, height: 800);
        return new KanbanFixture(clock, fake, catalog, board, window, view, steering, details);
    }

    /// <summary>Lets the dispatcher and layout settle after a change.</summary>
    internal void Settle() => ViewHarness.Settle(Window);

    /// <summary>Closes the window and stops the models.</summary>
    public void Dispose()
    {
        Window.Close();
        Board.Dispose();
        Catalog.Dispose();
    }
}
