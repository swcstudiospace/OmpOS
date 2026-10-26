using System.Runtime.CompilerServices;
using OmpOS.Contracts.Agents;
using OmpOS.EngineHost;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.AgentLoop.Steering;
using OmpOS.Native.Engine;
using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Approvals;

namespace OmpOS.Native.Agents;

/// <summary>
/// The head's side of one agent session: its live scope, its launch plan and stage sources (decided once, the first time anything needs
/// them, because the Substrate health check is asynchronous) and its engine (composed the first time a prompt is sent, and again after the
/// session is closed and reopened). The view of the session is built over the delegates of this slot, which stay valid while the engine
/// behind them is replaced, so closing and reopening never rebuilds the view or the observers.
/// </summary>
internal sealed class SessionSlot : IAsyncDisposable
{
    private const string ClosedMessage = "This agent is closed. Reopen it to continue.";

    private readonly SessionEngineSet _owner;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly System.Threading.Lock _gate = new();
    private Task<SessionPlan>? _planTask;
    private Task<EngineComposition>? _engineTask;
    private EngineComposition? _engine;
    private bool _engineClosed;
    private int _turnActive;
    private int _disposed;

    /// <summary>Creates the slot of a session.</summary>
    /// <param name="owner">The engine set that owns the slot.</param>
    /// <param name="scope">The live scope of the session.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public SessionSlot(SessionEngineSet owner, SessionScopeProvider scope)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(scope);
        _owner = owner;
        Scope = scope;
        Id = scope.Current.SessionId;
        Prompts = new SlotPromptChannel(this);
        Approvals = new SlotApprovalBridge(this);
        Stages = new DeferredStageSource(this);
        Context = new DeferredTaskContextSource(this);
        Steering = new FileSteeringChannel(scope.Current.SessionStateDirectory, owner.Clock, owner.SteeringOptions);
        Models = new SlotModelControl(ConnectModelsAsync);
    }

    /// <summary>Gets the session identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the live scope of the session.</summary>
    public SessionScopeProvider Scope { get; }

    /// <summary>Gets the send path of the session.</summary>
    public IPromptChannel Prompts { get; }

    /// <summary>Gets the approval bridge of the session.</summary>
    public IToolApprovalBridge Approvals { get; }

    /// <summary>Gets the stage source of the session.</summary>
    public IStageSource Stages { get; }

    /// <summary>Gets the task context source of the session.</summary>
    public ITaskContextSource Context { get; }

    /// <summary>Gets the steering channel of the session: a message put into it reaches the omp plugin of this session's omp process, which acknowledges it. It is used by the composer and by the Kanban card.</summary>
    public ISteeringChannel Steering { get; }

    /// <summary>Gets the model control of the session: it lists the models omp offers and switches the current one, whichever engine the slot holds now. The model chip of a chat session uses it.</summary>
    public SlotModelControl Models { get; }

    /// <summary>Gets or sets the runtime that shows the session. The engine set sets it once, right after it creates the runtime.</summary>
    public AgentSessionRuntime? Runtime { get; set; }

    /// <summary>Gets a value indicating whether a turn is running, so the heartbeat keeps the session from being marked lost.</summary>
    public bool TurnActive => Volatile.Read(ref _turnActive) != 0;

    /// <summary>Gets a value indicating whether the engine was stopped by closing the session and has not been reopened.</summary>
    public bool EngineClosed
    {
        get
        {
            lock (_gate)
            {
                return _engineClosed;
            }
        }
    }

    /// <summary>Records that a turn started or ended.</summary>
    /// <param name="active">True while a turn runs.</param>
    public void SetTurnActive(bool active) => Volatile.Write(ref _turnActive, active ? 1 : 0);

    /// <summary>Returns the launch plan and stage sources of the session, deciding them on the first call. A failed attempt is not kept: the next call tries again.</summary>
    /// <param name="cancellationToken">Stops waiting for the plan. The plan itself is built on the slot's own lifetime.</param>
    /// <returns>The plan.</returns>
    public async Task<SessionPlan> PlanAsync(CancellationToken cancellationToken)
    {
        Task<SessionPlan> task;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (_planTask is null || _planTask.IsFaulted || _planTask.IsCanceled)
            {
                _planTask = _owner.BuildPlanAsync(this, _lifetime.Token);
            }

            task = _planTask;
        }

        return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns the engine of the session, composing it on the first call after the session was created or reopened.</summary>
    /// <param name="cancellationToken">Stops waiting for the engine.</param>
    /// <returns>The engine. It starts no process until a prompt is submitted.</returns>
    /// <exception cref="InvalidOperationException">The session is closed.</exception>
    public async Task<EngineComposition> EngineAsync(CancellationToken cancellationToken)
    {
        Task<EngineComposition> task;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (_engineClosed)
            {
                throw new InvalidOperationException(ClosedMessage);
            }

            if (_engineTask is null || _engineTask.IsFaulted || _engineTask.IsCanceled)
            {
                _engineTask = ComposeEngineAsync();
            }

            task = _engineTask;
        }

        return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stops the engine and keeps the slot, so the session can be reopened. A turn in flight ends with the engine.</summary>
    /// <returns>A task that completes when the engine host is disposed.</returns>
    public async Task CloseEngineAsync()
    {
        Task<EngineComposition>? task;
        lock (_gate)
        {
            _engineClosed = true;
            task = _engineTask;
            _engineTask = null;
        }

        SetTurnActive(false);
        await DisposeEngineAsync(task).ConfigureAwait(false);
        lock (_gate)
        {
            _engine = null;
        }

        Models.Bind(null);
    }

    /// <summary>Records that the session is closed without an engine to stop, so a prompt is refused until the session is reopened. The engine set uses it for a session that was closed before the application started.</summary>
    public void MarkEngineClosed()
    {
        lock (_gate)
        {
            _engineClosed = true;
        }
    }

    /// <summary>Allows the session to compose a new engine on its next prompt.</summary>
    public void ReopenEngine()
    {
        lock (_gate)
        {
            _engineClosed = false;
        }
    }

    /// <summary>Publishes what a re-check of one plugin source found, and for the Substrate asks it again.</summary>
    /// <param name="source">The plugin source to check.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the result was published.</returns>
    /// <exception cref="InvalidOperationException">The source is not a plugin.</exception>
    public async Task RecheckAsync(StageSource source, CancellationToken cancellationToken)
    {
        var plugins = await _owner.RefreshPluginsAsync(cancellationToken).ConfigureAwait(false);
        var plan = await PlanAsync(cancellationToken).ConfigureAwait(false);
        var plugin = plugins.FirstOrDefault(candidate => candidate.Descriptor.StageSource == source)
            ?? throw new InvalidOperationException("That stage is not a plugin.");
        var (status, detail) = await _owner.DescribeRecheckAsync(plugin, plan, cancellationToken).ConfigureAwait(false);
        plan.Recheck.Publish(new StageEvent
        {
            SessionId = Id,
            Source = source,
            StageId = "plugin",
            Title = plugin.Descriptor.DisplayName + " plugin",
            Status = status,
            Timestamp = _owner.Now,
            Detail = detail,
            GraphId = Scope.Current.GraphId,
        });
    }

    /// <summary>Records a steering entry in the Substrate ledger. It fails when the Substrate is not active for the session.</summary>
    /// <param name="entry">The accepted steering entry.</param>
    /// <param name="cancellationToken">Cancels the recording.</param>
    /// <returns>A task that completes when the ledger stored the record.</returns>
    /// <exception cref="InvalidOperationException">The Substrate is not active for the session, or the ledger did not store the record.</exception>
    public async Task RecordSteeringAsync(SteeringEntry entry, CancellationToken cancellationToken)
    {
        var plan = await PlanAsync(cancellationToken).ConfigureAwait(false);
        var steering = plan.Steering ?? throw new InvalidOperationException("The Substrate is not active for this agent, so the message was not recorded.");
        await steering.RecordAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Called with a Graph identifier the planner reported.</summary>
    /// <param name="graphId">The Graph identifier.</param>
    public void OnGraphId(string graphId)
    {
        if (Scope.Update(null, graphId))
        {
            _owner.Registry?.SetGraphId(Id, graphId);
        }
    }

    /// <summary>Stops the engine and releases the plan. The runtime that shows the slot is disposed by the engine set.</summary>
    /// <returns>A task that completes when everything is released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Task<EngineComposition>? engine;
        Task<SessionPlan>? plan;
        lock (_gate)
        {
            engine = _engineTask;
            plan = _planTask;
            _engineTask = null;
            _engine = null;
            _planTask = null;
        }

        await _lifetime.CancelAsync().ConfigureAwait(false);
        await DisposeEngineAsync(engine).ConfigureAwait(false);
        Models.Bind(null);
        if (plan is not null)
        {
            try
            {
                (await plan.ConfigureAwait(false)).Dispose();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // The plan never finished building, so it holds nothing to release.
            }
        }

        _lifetime.Dispose();
    }

    private EngineComposition? TryGetEngine()
    {
        lock (_gate)
        {
            return _engineClosed ? null : _engine;
        }
    }

    /// <summary>Starts the engine session of the slot so that its models are known: composes the engine when needed, then opens its omp session. It starts no prompt.</summary>
    private async Task ConnectModelsAsync(CancellationToken cancellationToken)
    {
        var engine = await EngineAsync(cancellationToken).ConfigureAwait(false);
        await engine.Host.ConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<EngineComposition> ComposeEngineAsync()
    {
        var plan = await PlanAsync(CancellationToken.None).ConfigureAwait(false);
        string? resume = null;
        if (_owner.Registry is { } registry && registry.TryGet(Id, out var record))
        {
            resume = record.ResumeReference;
        }

        var engine = _owner.ComposeEngine(plan.Launch, resume);
        engine.Host.NativeSessionIdChanged += OnNativeSessionIdChanged;
        lock (_gate)
        {
            _engine = engine;
        }

        Models.Bind(engine.Host.Models);

        return engine;
    }

    private async Task DisposeEngineAsync(Task<EngineComposition>? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            var engine = await task.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            engine.Host.NativeSessionIdChanged -= OnNativeSessionIdChanged;
            await engine.Host.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The engine never finished composing, or its host was already gone: there is no process to stop.
        }
    }

    private void OnNativeSessionIdChanged(object? sender, EventArgs e)
    {
        if (sender is not InProcessEngineHost { NativeSessionId: { Length: > 0 } nativeId })
        {
            return;
        }

        Scope.Update(nativeId, null);
        _owner.Registry?.SetAcpSession(Id, nativeId, nativeId);
    }

    /// <summary>The send path of the slot: it forwards to whichever engine the slot holds when the prompt is sent.</summary>
    private sealed class SlotPromptChannel(SessionSlot slot) : IPromptChannel
    {
        public async IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            var engine = await slot.EngineAsync(cancellationToken).ConfigureAwait(false);
            await foreach (var harnessEvent in engine.Host.SubmitPromptAsync(prompt, new PromptOptions(), cancellationToken).ConfigureAwait(false))
            {
                slot._owner.Registry?.Touch(slot.Id);
                yield return harnessEvent;
            }
        }

        public Task CancelAsync(CancellationToken cancellationToken) =>
            slot.TryGetEngine() is { } engine ? engine.Host.CancelAsync(cancellationToken) : Task.CompletedTask;

        // The slot owns the engine, so closing the send path of a runtime stops nothing.
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>The approval bridge of the slot: a decision reaches the engine that raised the request.</summary>
    private sealed class SlotApprovalBridge(SessionSlot slot) : IToolApprovalBridge
    {
        public Task<ToolApprovalOutcome> DecideAsync(string sessionId, string requestId, ToolApprovalDecision decision, CancellationToken cancellationToken) =>
            slot.TryGetEngine() is { } engine
                ? engine.ApprovalBridge.DecideAsync(sessionId, requestId, decision, cancellationToken)
                : Task.FromResult(ToolApprovalOutcome.AlreadyDecided);
    }

    /// <summary>The stage source of the slot: it waits for the plan, then observes the plan's merged source.</summary>
    private sealed class DeferredStageSource(SessionSlot slot) : IStageSource
    {
        public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(scope);
            SessionPlan? plan;
            try
            {
                plan = await slot.PlanAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                plan = null;
            }

            if (plan is null)
            {
                yield break;
            }

            await foreach (var stageEvent in plan.Stages.ObserveAsync(scope, cancellationToken).ConfigureAwait(false))
            {
                yield return stageEvent;
            }
        }
    }

    /// <summary>The task context source of the slot: it waits for the plan, then reads the plan's context.</summary>
    private sealed class DeferredTaskContextSource(SessionSlot slot) : ITaskContextSource
    {
        public async Task<TaskContext> GetAsync(StageObservationScope scope, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(scope);
            var plan = await slot.PlanAsync(cancellationToken).ConfigureAwait(false);
            return await plan.Context.GetAsync(scope, cancellationToken).ConfigureAwait(false);
        }
    }
}
