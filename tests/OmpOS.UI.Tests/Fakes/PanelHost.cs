using OmpOS.Contracts.Agents;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Runs;
using OmpOS.Presentation.State;

namespace OmpOS.UI.Tests.Fakes;

/// <summary>The host behind the Agent Panel view model: it builds the session view models, records what the panel asked and lets a test make an action fail.</summary>
internal sealed class PanelHost : IAgentPanelHost
{
    private readonly PanelCatalog _catalog;
    private readonly PanelClock _clock;
    private int _created;

    /// <summary>Creates a host over the catalog and the clock of a harness.</summary>
    /// <param name="catalog">The catalog new sessions are put in.</param>
    /// <param name="clock">The clock the session view models read.</param>
    internal PanelHost(PanelCatalog catalog, PanelClock clock)
    {
        _catalog = catalog;
        _clock = clock;
    }

    /// <summary>Gets the identifiers of the sessions created, in order.</summary>
    internal List<string> Created { get; } = [];

    /// <summary>Gets the identifiers of the omp terminal sessions created, in order.</summary>
    internal List<string> CreatedTerminals { get; } = [];

    /// <summary>Gets the identifiers of the sessions closed, in order.</summary>
    internal List<string> Closed { get; } = [];

    /// <summary>Gets the number of reloads.</summary>
    internal int ReloadCount { get; private set; }

    /// <summary>Gets the messages sent from any composer, in order.</summary>
    internal List<string> Sent { get; } = [];

    /// <summary>Gets the number of times a composer asked to stop.</summary>
    internal int Stops { get; private set; }

    /// <summary>Gets the number of resumes and reopens the sessions were asked for.</summary>
    internal int Resumes { get; private set; }

    /// <summary>Gets the plugin sources a stage re-check was requested for, in order.</summary>
    internal List<StageSource> Rechecks { get; } = [];

    /// <summary>Gets the session view models built, by session identifier.</summary>
    internal Dictionary<string, AgentSessionViewModel> ViewModels { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the run schedulers of the sessions, by session identifier; a test starts a run through one.</summary>
    internal Dictionary<string, RunInputScheduler> Schedulers { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the engine input of the sessions, by session identifier.</summary>
    internal Dictionary<string, PanelRunInput> Inputs { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the exception <see cref="CloseSessionAsync"/> throws, or null.</summary>
    internal Exception? CloseFailure { get; set; }

    /// <summary>Gets or sets the exception <see cref="CreateSessionAsync"/> throws, or null.</summary>
    internal Exception? CreateFailure { get; set; }

    /// <summary>Gets or sets the bridge approvals are decided through, or null for sessions that cannot decide approvals. Set it before a session is opened.</summary>
    internal PanelBridge? Bridge { get; set; }

    /// <summary>Gets or sets the live steering delegate handed to new sessions, or null for the scheduler's queue-after-turn path. Set it before a session is opened.</summary>
    internal Func<string, SteeringMode, CancellationToken, Task<SteeringDelivery>>? Live { get; set; }

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
        _catalog.Put(Summary(id, SessionLifecycle.Created, now, now));
        return Task.FromResult(id);
    }

    /// <inheritdoc />
    public Task<string> CreateTerminalSessionAsync(CancellationToken cancellationToken)
    {
        _created++;
        var id = $"terminal-{_created}";
        var now = _clock.GetUtcNow();
        CreatedTerminals.Add(id);
        _catalog.Put(Summary(id, SessionLifecycle.Created, now, now) with { Kind = SessionKind.Terminal });
        return Task.FromResult(id);
    }

    /// <inheritdoc />
    public AgentSessionViewModel OpenSession(AgentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var input = new PanelRunInput();
        var scheduler = new RunInputScheduler(input);
        var viewModel = new AgentSessionViewModel(new AgentSessionOptions
        {
            Row = row,
            Session = new SessionViewModel(),
            Composer = new ComposerViewModel(
                text =>
                {
                    Sent.Add(text);
                    return Task.FromResult(true);
                },
                () =>
                {
                    Stops++;
                    return Task.CompletedTask;
                }),
            Scheduler = scheduler,
            Scope = new StageObservationScope
            {
                SessionId = row.Id,
                WorkspaceRoot = "/workspace",
                SessionStateDirectory = "/state/" + row.Id,
            },
            TimeProvider = _clock,
            ApprovalCoordinator = Bridge is null ? null : new ToolApprovalCoordinator(Bridge),
            LiveSteering = Live,
            StageRecheck = (source, _) =>
            {
                Rechecks.Add(source);
                return Task.CompletedTask;
            },
            Resume = _ =>
            {
                Resumes++;
                return Task.CompletedTask;
            },
            Reopen = _ =>
            {
                Resumes++;
                return Task.CompletedTask;
            },
        });
        ViewModels[row.Id] = viewModel;
        Schedulers[row.Id] = scheduler;
        Inputs[row.Id] = input;
        return viewModel;
    }

    /// <inheritdoc />
    public Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        Closed.Add(sessionId);
        return CloseFailure is null ? Task.CompletedTask : Task.FromException(CloseFailure);
    }

    /// <inheritdoc />
    public Task ReloadAsync(CancellationToken cancellationToken)
    {
        ReloadCount++;
        return Task.CompletedTask;
    }

    /// <summary>Builds a session summary.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="lifecycle">The persisted lifecycle.</param>
    /// <param name="created">When it was created.</param>
    /// <param name="lastActivity">When it last did something.</param>
    /// <param name="title">The title; it defaults to <c>Agent {id}</c>.</param>
    /// <returns>The summary.</returns>
    internal static SessionSummary Summary(string id, SessionLifecycle lifecycle, DateTimeOffset created, DateTimeOffset lastActivity, string? title = null) => new()
    {
        Id = id,
        Title = title ?? $"Agent {id}",
        Lifecycle = lifecycle,
        CreatedAt = created,
        LastActivity = lastActivity,
        WorkspaceRoot = "/workspace/demo",
    };
}
