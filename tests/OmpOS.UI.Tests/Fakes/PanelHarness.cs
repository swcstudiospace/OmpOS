using OmpOS.Contracts.Agents;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Kanban;
using Shouldly;

namespace OmpOS.UI.Tests.Fakes;

/// <summary>An Agent Panel view model over a scripted catalog and host, with the clock under the test's control.</summary>
internal sealed class PanelHarness : IDisposable
{
    private readonly SessionCatalogModel _model;
    private KanbanBoardModel? _board;

    private PanelHarness(PanelCatalog catalog, PanelClock clock, PanelHost host)
    {
        Catalog = catalog;
        Clock = clock;
        Host = host;
        _model = new SessionCatalogModel(catalog, clock);
        Panel = new AgentPanelViewModel(_model, host);
    }

    /// <summary>Gets the catalog the rail lists.</summary>
    internal PanelCatalog Catalog { get; }

    /// <summary>Gets the clock.</summary>
    internal PanelClock Clock { get; }

    /// <summary>Gets the host.</summary>
    internal PanelHost Host { get; }

    /// <summary>Gets the Agent Panel view model under test.</summary>
    internal AgentPanelViewModel Panel { get; }

    /// <summary>Creates a harness whose catalog lists the given sessions, each last active two seconds ago.</summary>
    /// <param name="sessions">The session identifier, lifecycle and optional title of each session.</param>
    /// <returns>The harness.</returns>
    internal static PanelHarness With(params (string Id, SessionLifecycle Lifecycle, string? Title)[] sessions)
    {
        var catalog = new PanelCatalog();
        var clock = new PanelClock();
        var now = clock.GetUtcNow();
        foreach (var (id, lifecycle, title) in sessions)
        {
            catalog.Sessions.Add(PanelHost.Summary(id, lifecycle, now.AddMinutes(-5), now.AddSeconds(-2), title));
        }

        return new PanelHarness(catalog, clock, new PanelHost(catalog, clock));
    }

    /// <summary>Selects a session and waits until its view model is bound.</summary>
    /// <param name="id">The session identifier.</param>
    /// <returns>The selected session view model.</returns>
    internal async Task<AgentSessionViewModel> SelectAsync(string id)
    {
        Panel.SelectSession(id).ShouldBeTrue();
        await Panel.SelectionLoad.WaitAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
        return Panel.Selected.ShouldNotBeNull();
    }

    /// <summary>Gets the Kanban board over the same catalog, created on first use and disposed with the harness.</summary>
    internal KanbanBoardModel Board => _board ??= new KanbanBoardModel(_model, Clock);

    /// <inheritdoc />
    public void Dispose()
    {
        _board?.Dispose();
        Panel.Dispose();
        _model.Dispose();
    }
}
