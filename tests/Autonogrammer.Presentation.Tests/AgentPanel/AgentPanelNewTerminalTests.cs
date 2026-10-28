using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.AgentPanel;

/// <summary>New omp terminal: the panel asks the host for a terminal session, selects it, reports a failure, and the rail row says it is a terminal.</summary>
public sealed class AgentPanelNewTerminalTests : IDisposable
{
    private readonly ManualTimeProvider _clock = new();
    private readonly FakeSessionCatalog _catalog = new();
    private readonly ScriptedPanelHost _host;
    private readonly SessionCatalogModel _model;
    private readonly AgentPanelViewModel _panel;

    /// <summary>Creates a panel over an empty registry.</summary>
    public AgentPanelNewTerminalTests()
    {
        _host = new ScriptedPanelHost(_catalog, _clock);
        _model = new SessionCatalogModel(_catalog, _clock, new SessionCatalogOptions { IsClosed = id => _host.ClosedIds.Contains(id) });
        _panel = new AgentPanelViewModel(_model, _host);
    }

    /// <summary>Releases the panel and the catalog model.</summary>
    public void Dispose()
    {
        _panel.Dispose();
        _model.Dispose();
    }

    /// <summary>New omp terminal creates a terminal session through the host, not a chat, and selects its row.</summary>
    [Fact]
    public async Task NewTerminalCreatesAndSelectsATerminalSession()
    {
        await _panel.NewTerminalCommand.ExecuteAsync(null).ConfigureAwait(true);

        _host.CreatedTerminals.ShouldBe(["terminal-1"]);
        _host.Created.ShouldBeEmpty("a terminal is not a chat session");
        _panel.State.ShouldBe(PanelState.Ready);
        _panel.Selected.ShouldNotBeNull().Id.ShouldBe("terminal-1");
        _panel.Selected.Row.IsTerminal.ShouldBeTrue();
        _panel.Selected.Row.Summary.Kind.ShouldBe(SessionKind.Terminal);
    }

    /// <summary>A chat row is not a terminal row, and a terminal row names itself in its accessible name beside its status.</summary>
    [Fact]
    public async Task OnlyTerminalRowsAreTaggedAsTerminals()
    {
        await _panel.NewSessionCommand.ExecuteAsync(null).ConfigureAwait(true);
        await _panel.NewTerminalCommand.ExecuteAsync(null).ConfigureAwait(true);

        var chat = _model.Sessions.Single(row => string.Equals(row.Id, "new-1", StringComparison.Ordinal));
        var terminal = _model.Sessions.Single(row => string.Equals(row.Id, "terminal-2", StringComparison.Ordinal));
        chat.IsTerminal.ShouldBeFalse();
        chat.AccessibleName.ShouldNotContain("terminal", Case.Insensitive);
        terminal.IsTerminal.ShouldBeTrue();
        terminal.AccessibleName.ShouldContain(", terminal", Case.Sensitive);
        terminal.AccessibleName.ShouldStartWith($"{terminal.Title}, {terminal.StatusText}", Case.Sensitive);
    }

    /// <summary>A host that cannot create a terminal leaves the panel as it was and says why, then recovers.</summary>
    [Fact]
    public async Task AFailedNewTerminalIsReportedThenRecovers()
    {
        _host.CreateFailure = new IOException("no pty available");

        await _panel.NewTerminalCommand.ExecuteAsync(null).ConfigureAwait(true);

        _panel.ActionError.ShouldBe("Could not start a new omp terminal. no pty available.");
        _panel.State.ShouldBe(PanelState.Empty);
        _panel.Selected.ShouldBeNull();

        _host.CreateFailure = null;
        await _panel.NewTerminalCommand.ExecuteAsync(null).ConfigureAwait(true);

        _panel.ActionError.ShouldBeNull();
        _panel.Selected.ShouldNotBeNull().Id.ShouldBe("terminal-1", "the failed attempt consumed no identifier");
    }
}
