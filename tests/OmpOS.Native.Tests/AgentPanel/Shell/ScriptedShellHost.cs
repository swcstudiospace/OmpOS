using OmpOS.Contracts.Agents;
using OmpOS.Native.Tests.Terminal;
using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>
/// The head of a shell under test: it creates one <see cref="AgentSessionRuntime"/> per session over the session's scripted send path,
/// the way the production head creates one engine per session, and it records what the panel asked of it.
/// </summary>
internal sealed class ScriptedShellHost(FakeAgentCatalog catalog, ShellClock clock) : IAgentPanelHost
{
    private readonly Dictionary<string, SessionScript> _scripts = new(StringComparer.Ordinal);
    private int _created;

    /// <summary>Gets the runtimes the panel opened, by session identifier.</summary>
    public Dictionary<string, AgentSessionRuntime> Runtimes { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the identifiers of the sessions the panel asked to close, in order.</summary>
    public List<string> Closed { get; } = [];

    /// <summary>Gets the identifiers of the sessions whose engine the host stopped. The catalog model reads this as the closed flag.</summary>
    public HashSet<string> ClosedIds { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets how many times the panel asked the host to reload.</summary>
    public int ReloadCount { get; private set; }

    /// <summary>Gets or sets the failure the next reload throws, or null when it succeeds.</summary>
    public Exception? ReloadFailure { get; set; }

    /// <summary>Gets the plugin sources the panel asked the host to re-check, with their session.</summary>
    public List<(string SessionId, StageSource Source)> Rechecked { get; } = [];

    /// <summary>Gets or sets the script of a session that New agent creates. Null creates a session over an unconnected send path.</summary>
    public Func<string, SessionScript>? NewSessionScript { get; set; }

    /// <summary>Gets the identifiers of the terminal sessions New omp terminal created, in order.</summary>
    public List<string> CreatedTerminals { get; } = [];

    /// <summary>
    /// Gets or sets the script of a terminal session that New omp terminal creates. Null creates a terminal over a fake omp starter and a
    /// fake steering channel.
    /// </summary>
    public Func<string, SessionScript>? NewTerminalScript { get; set; }

    /// <summary>Registers the script of a session before the panel opens it.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="script">The script.</param>
    public void Script(string sessionId, SessionScript script) => _scripts[sessionId] = script;

    /// <summary>Gets the script of a session, creating a default one over an unconnected send path.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The script.</returns>
    public SessionScript ScriptOf(string sessionId)
    {
        if (!_scripts.TryGetValue(sessionId, out var script))
        {
            script = new SessionScript();
            _scripts[sessionId] = script;
        }

        return script;
    }

    /// <inheritdoc />
    public Task<string> CreateSessionAsync(CancellationToken cancellationToken)
    {
        _created++;
        var id = $"new-{_created}";
        var now = clock.Now;
        if (NewSessionScript is { } factory)
        {
            _scripts[id] = factory(id);
        }

        catalog.Put(new SessionSummary
        {
            Id = id,
            Title = $"New agent {_created}",
            Lifecycle = SessionLifecycle.Created,
            CreatedAt = now,
            LastActivity = now,
        });
        return Task.FromResult(id);
    }

    /// <inheritdoc />
    public Task<string> CreateTerminalSessionAsync(CancellationToken cancellationToken)
    {
        _created++;
        var id = $"terminal-{_created}";
        var now = clock.Now;
        _scripts[id] = NewTerminalScript?.Invoke(id)
            ?? new SessionScript { TerminalStarter = new FakeOmpTerminalStarter(), Steering = new FakeSteeringChannel() };
        CreatedTerminals.Add(id);
        catalog.Put(new SessionSummary
        {
            Id = id,
            Title = $"New omp terminal {_created}",
            Lifecycle = SessionLifecycle.Created,
            CreatedAt = now,
            LastActivity = now,
            Kind = SessionKind.Terminal,
        });
        return Task.FromResult(id);
    }

    /// <inheritdoc />
    public AgentSessionViewModel OpenSession(AgentRow row)
    {
        var script = ScriptOf(row.Id);
        var runtime = new AgentSessionRuntime(new AgentSessionRuntimeOptions
        {
            Row = row,
            Prompts = script.Prompts,
            Approvals = script.Approvals,
            Scope = new StageObservationScope
            {
                SessionId = row.Id,
                WorkspaceRoot = Environment.CurrentDirectory,
                SessionStateDirectory = Path.Combine(Path.GetTempPath(), "ompos-shell-tests", row.Id),
            },
            Stages = script.Stages,
            Context = script.Context,
            RecordSteering = script.RecordSteering,
            Steering = script.Steering,
            TerminalStarter = script.TerminalStarter,
            SessionCommands = script.SessionCommands,
            MentionSources = script.MentionSources,
            Forker = script.Forker,
            LiveEvents = script.LiveEvents,
            Reopen = _ => ReopenAsync(row.Id),
            Resume = _ => ResumeAsync(row.Id),
            StageRecheck = (source, _) =>
            {
                Rechecked.Add((row.Id, source));
                return Task.CompletedTask;
            },
            TimeProvider = clock,
        });
        Runtimes[row.Id] = runtime;
        runtime.StartObserving();
        return runtime.ViewModel;
    }

    /// <inheritdoc />
    public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        Closed.Add(sessionId);
        ClosedIds.Add(sessionId);

        // Closing a session stops its engine: the turn in flight ends the way a stopped engine ends it.
        if (Runtimes.TryGetValue(sessionId, out var runtime) && runtime.Composer.IsStreaming)
        {
            await ScriptOf(sessionId).Prompts.CancelAsync(cancellationToken).ConfigureAwait(true);
        }

        if (catalog.Find(sessionId) is { } summary)
        {
            catalog.Put(summary with { Lifecycle = SessionLifecycle.Paused });
        }
    }

    /// <inheritdoc />
    public Task ReloadAsync(CancellationToken cancellationToken)
    {
        ReloadCount++;
        return ReloadFailure is null ? Task.CompletedTask : Task.FromException(ReloadFailure);
    }

    private Task ResumeAsync(string sessionId)
    {
        if (catalog.Find(sessionId) is { } summary)
        {
            catalog.Put(summary with { Lifecycle = SessionLifecycle.Running });
        }

        return Task.CompletedTask;
    }

    private Task ReopenAsync(string sessionId)
    {
        ClosedIds.Remove(sessionId);
        if (catalog.Find(sessionId) is { } summary)
        {
            catalog.Put(summary with { Lifecycle = SessionLifecycle.Running });
        }

        return Task.CompletedTask;
    }
}
