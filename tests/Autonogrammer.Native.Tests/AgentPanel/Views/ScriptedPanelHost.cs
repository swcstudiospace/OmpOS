using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.Runs;
using Autonogrammer.Presentation.State;

namespace Autonogrammer.Native.Tests.AgentPanel.Views;

/// <summary>A host that stands in for the head: it creates and closes sessions in a fake registry and builds each session's view model.</summary>
internal sealed class ScriptedPanelHost : IAgentPanelHost
{
    private readonly FakeSessionCatalog _catalog;
    private readonly ManualTimeProvider _clock;
    private int _created;

    /// <summary>Creates the host over the registry the panel's catalog reads.</summary>
    /// <param name="catalog">The fake registry.</param>
    /// <param name="clock">The clock the view models use.</param>
    internal ScriptedPanelHost(FakeSessionCatalog catalog, ManualTimeProvider clock)
    {
        _catalog = catalog;
        _clock = clock;
    }

    /// <summary>Gets the identifiers of the sessions created, in order.</summary>
    internal List<string> Created { get; } = [];

    /// <summary>Gets the identifiers of the omp terminal sessions created, in order.</summary>
    internal List<string> CreatedTerminals { get; } = [];

    /// <summary>Gets the identifiers passed to <see cref="OpenSession"/>, in order.</summary>
    internal List<string> Opened { get; } = [];

    /// <summary>Gets the identifiers passed to <see cref="CloseSessionAsync"/>, in order.</summary>
    internal List<string> Closed { get; } = [];

    /// <summary>Gets the identifiers of the sessions whose engine is closed.</summary>
    internal HashSet<string> ClosedIds { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the view models built, by session identifier.</summary>
    internal Dictionary<string, AgentSessionViewModel> ViewModels { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the number of reloads.</summary>
    internal int ReloadCount { get; private set; }

    /// <summary>Gets or sets the exception <see cref="CreateSessionAsync"/> throws, or null.</summary>
    internal Exception? CreateFailure { get; set; }

    /// <summary>Gets or sets the exception <see cref="CloseSessionAsync"/> throws, or null.</summary>
    internal Exception? CloseFailure { get; set; }

    /// <summary>Gets or sets the exception <see cref="ReloadAsync"/> throws, or null.</summary>
    internal Exception? ReloadFailure { get; set; }

    /// <summary>Gets or sets a task <see cref="ReloadAsync"/> waits for, or null.</summary>
    internal Task? ReloadGate { get; set; }

    /// <summary>Gets or sets the source of the task context of each session, or null for none.</summary>
    internal Func<string, ITaskContextSource?>? ContextSources { get; set; }

    /// <inheritdoc />
    public Task<string> CreateSessionAsync(CancellationToken cancellationToken)
    {
        if (CreateFailure is not null)
        {
            return Task.FromException<string>(CreateFailure);
        }

        _created++;
        var id = $"new-{_created}";
        var now = _clock.GetUtcNow();
        Created.Add(id);
        _catalog.Put(PanelFixtures.Summary(id, SessionLifecycle.Created, now, now));
        return Task.FromResult(id);
    }

    /// <inheritdoc />
    public Task<string> CreateTerminalSessionAsync(CancellationToken cancellationToken)
    {
        if (CreateFailure is not null)
        {
            return Task.FromException<string>(CreateFailure);
        }

        _created++;
        var id = $"terminal-{_created}";
        var now = _clock.GetUtcNow();
        CreatedTerminals.Add(id);
        _catalog.Put(PanelFixtures.Summary(id, SessionLifecycle.Created, now, now) with { Kind = SessionKind.Terminal });
        return Task.FromResult(id);
    }

    /// <inheritdoc />
    public AgentSessionViewModel OpenSession(AgentRow row)
    {
        Opened.Add(row.Id);
        var composer = new ComposerViewModel(_ => Task.FromResult(true), () => Task.CompletedTask);
        var viewModel = new AgentSessionViewModel(new AgentSessionOptions
        {
            Row = row,
            Session = new SessionViewModel(),
            Composer = composer,
            Scheduler = new RunInputScheduler(new ScriptedRunInput()),
            Scope = PanelFixtures.Scope(row.Id),
            TimeProvider = _clock,
            TaskContextSource = ContextSources?.Invoke(row.Id),
        });
        ViewModels[row.Id] = viewModel;
        return viewModel;
    }

    /// <inheritdoc />
    public Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        Closed.Add(sessionId);
        if (CloseFailure is not null)
        {
            return Task.FromException(CloseFailure);
        }

        ClosedIds.Add(sessionId);
        var existing = _catalog.Sessions.Single(summary => string.Equals(summary.Id, sessionId, StringComparison.Ordinal));
        _catalog.Put(existing with { Lifecycle = SessionLifecycle.Paused });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        ReloadCount++;
        if (ReloadGate is not null)
        {
            await ReloadGate.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        if (ReloadFailure is not null)
        {
            throw ReloadFailure;
        }
    }
}
