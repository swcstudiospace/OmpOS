using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.AgentPanel;

/// <summary>Retired terminal records: the panel has no creation path for them, and a persisted terminal row lists as retired, never as chat.</summary>
public sealed class AgentPanelRetiredTerminalTests : IDisposable
{
    private readonly ManualTimeProvider _clock = new();
    private readonly FakeSessionCatalog _catalog = new();
    private readonly ScriptedPanelHost _host;
    private readonly SessionCatalogModel _model;
    private readonly AgentPanelViewModel _panel;

    /// <summary>Creates a panel over an empty registry.</summary>
    public AgentPanelRetiredTerminalTests()
    {
        _host = new ScriptedPanelHost(_catalog, _clock);
        _model = new SessionCatalogModel(_catalog, _clock, new SessionCatalogOptions { IsClosed = id => _host.ClosedIds.Contains(id) });
        _panel = new AgentPanelViewModel(_model, _host);
    }

    /// <summary>A persisted terminal row lists with the retired signal: word tag, retired status words, and a terminal accessible name.</summary>
    [Fact]
    public void APersistedTerminalRowListsAsRetired()
    {
        var now = _clock.GetUtcNow();
        _catalog.Put(AgentPanelTestData.Summary("old-terminal", SessionLifecycle.Completed, now.AddMinutes(-30), now.AddMinutes(-1)) with { Kind = SessionKind.Terminal });
        _catalog.Put(AgentPanelTestData.Summary("a-chat", SessionLifecycle.Completed, now.AddMinutes(-30), now.AddMinutes(-1)));

        var terminal = _model.Rows.Single(row => string.Equals(row.Id, "old-terminal", StringComparison.Ordinal));
        var chat = _model.Rows.Single(row => string.Equals(row.Id, "a-chat", StringComparison.Ordinal));

        terminal.IsTerminal.ShouldBeTrue();
        terminal.Summary.Kind.ShouldBe(SessionKind.Terminal);
        terminal.AccessibleName.ShouldContain(", terminal", Case.Sensitive);
        terminal.AccessibleName.ShouldStartWith($"{terminal.Title}, {terminal.StatusText}", Case.Sensitive);
        chat.IsTerminal.ShouldBeFalse();
        chat.AccessibleName.ShouldNotContain("terminal", Case.Insensitive);
    }

    /// <summary>The only creation path left is a chat: New agent creates a chat session, not a terminal.</summary>
    [Fact]
    public async Task NewAgentCreatesAChatNotATerminal()
    {
        await _panel.NewSessionCommand.ExecuteAsync(null).ConfigureAwait(true);

        _host.Created.ShouldBe(["new-1"]);
        _panel.State.ShouldBe(PanelState.Ready);
        _panel.Selected.ShouldNotBeNull().Id.ShouldBe("new-1");
        _panel.Selected.Row.IsTerminal.ShouldBeFalse();
        _panel.Selected.Row.Summary.Kind.ShouldBe(SessionKind.Chat);
    }

    /// <summary>Releases the panel and the catalog model.</summary>
    public void Dispose()
    {
        _panel.Dispose();
        _model.Dispose();
    }
}
