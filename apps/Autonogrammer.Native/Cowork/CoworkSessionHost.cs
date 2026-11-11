using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Chat;
using Autonogrammer.Cowork.Sessions;
using Autonogrammer.EngineHost;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Logging;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Composer.Attachments;
using Avalonia.Threading;

namespace Autonogrammer.Native.Cowork;

/// <summary>The Cowork <see cref="IAgentPanelHost"/>: one registry and engine per opened task, local turn origin on every prompt. Startup, close and sign-out are serialized; disposal drains their cleanup before the head closes its borrowed tool server.</summary>
internal sealed class CoworkSessionHost : IAgentPanelHost, IAsyncDisposable
{
    private readonly CoworkSessionHostOptions _options;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Slot> _slots = new(StringComparer.Ordinal);
    private CoworkSessionRegistry? _registry;
    // The tail always completes successfully, even when an operation fails. Shutdown drains it before closing tools.
    private Task _operations = Task.CompletedTask;
    private Task? _disposeTask;
    private int _enginesStopped;
    private bool _disposed;

    /// <summary>Creates the host. It opens the registry on the first create or reload.</summary>
    /// <param name="options">The seams.</param>
    public CoworkSessionHost(CoworkSessionHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        Broker = new CoworkApprovalBroker(options.ToolOptions, options.Clock, options.WindowAvailable);
    }

    /// <summary>Gets how many times <see cref="CoworkSessionRegistry.RecoverAfterRestart"/> ran.</summary>
    public int Recoveries { get; private set; }

    /// <summary>Gets notices from composition and a degraded gateway.</summary>
    public IReadOnlyList<string> Notices { get; private set; } = [];

    /// <summary>Gets the approval broker.</summary>
    public CoworkApprovalBroker Broker { get; }

    /// <summary>Gets whether engines were stopped.</summary>
    public bool EnginesStopped => Volatile.Read(ref _enginesStopped) != 0;

    /// <summary>Gets the origins recorded for a session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The origins, in order.</returns>
    public IReadOnlyList<CoworkTurnOrigin> OriginsOf(string sessionId) => SlotOf(sessionId).Lease.Origins;

    /// <summary>Gets the conversation model for a session, or null when it is not open.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>Null until a presentation conversation model is bound. The queue is the broker's single card list.</returns>
    public object? ConversationOf(string sessionId)
    {
        lock (_gate)
        {
            return _slots.ContainsKey(sessionId) ? Broker.Cards : null;
        }
    }



    /// <inheritdoc />
    public async Task<string> CreateSessionAsync(CancellationToken cancellationToken)
    {
        var id = EnsureRegistry(cancellationToken).Create(null).Id;
        await Task.CompletedTask.ConfigureAwait(false);
        return id;
    }

    /// <inheritdoc />
    public Task<string> CreateTerminalSessionAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Cowork has no terminal sessions.");

    /// <inheritdoc />
    [SuppressMessage("Maintainability", "MA0051", Justification = "The acquire, transfer and cleanup steps stay in one flow so the cleanup guarantee sits beside the owners it protects.")]
    public async Task<AgentSessionViewModel> OpenSessionAsync(AgentRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        var completion = QueueOperation(out var previous);
        Slot? slot = null;
        var completed = false;
        try
        {
            await previous.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_slots.TryGetValue(row.Id, out var existing))
                {
                    completed = true;
                    return existing.Runtime.ViewModel;
                }
            }

            slot = await OpenCoreAsync(row, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var viewModel = slot.Runtime.ViewModel;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _slots.Add(row.Id, slot);
                slot = null;
                Volatile.Write(ref _enginesStopped, 0);
            }

            completed = true;
            return viewModel;
        }
        finally
        {
            ExceptionDispatchInfo? failure = null;
            try
            {
                if (slot is not null)
                {
                    await slot.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                if (slot is not null)
                {
                    _options.Tools?.CloseSession(row.Id);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            completion.SetResult();
            CleanupFailureReporter.Report(failure, completed, _options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Cowork"));
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: each owner is nulled once transferred to its composed holder; the finally disposes any owner still held directly.")]
    public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        var completion = TryQueueOperation(out var previous);
        if (completion is null)
        {
            // Disposal has stopped the engines. The close only records the pause, as the satellite does, so it does not throw.
            _options.Tools?.CloseSession(sessionId);
            _ = _registry?.SetStatus(sessionId, Contracts.Cowork.CoworkSessionStatus.Paused);
            return;
        }
        Slot? slot = null;
        var closeSession = false;
        var completed = false;
        try
        {
            await previous.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            closeSession = true;
            lock (_gate)
            {
                _ = _slots.Remove(sessionId, out slot);
            }
            completed = true;
        }
        finally
        {
            ExceptionDispatchInfo? failure = null;
            try
            {
                if (slot is not null)
                {
                    await slot.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                if (closeSession)
                {
                    _options.Tools?.CloseSession(sessionId);
                    _ = _registry?.SetStatus(sessionId, Contracts.Cowork.CoworkSessionStatus.Paused);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            completion.SetResult();
            CleanupFailureReporter.Report(failure, completed, _options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Cowork"));
        }
    }

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        _ = EnsureRegistry(cancellationToken);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>Sends a prompt after setting the local turn origin. A model argument named origin is ignored.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="prompt">The prompt.</param>
    /// <param name="arguments">Model-supplied arguments. An origin property is not forwarded.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>The task.</returns>
    public Task SendAsync(string sessionId, string prompt, JsonElement? arguments, CancellationToken cancellationToken) =>
        TurnAsync(sessionId, prompt, cancellationToken);

    /// <summary>Retries the last prompt with a fresh local origin.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="arguments">Model-supplied arguments.</param>
    /// <param name="cancellationToken">Cancels the retry.</param>
    /// <returns>The task.</returns>
    public Task RetryAsync(string sessionId, JsonElement? arguments, CancellationToken cancellationToken) =>
        OnUiThreadAsync(async () =>
        {
            // A run still going cannot be retried: the retry would replay the last prompt into the turn that is answering it.
            if (SlotOf(sessionId).Runtime.Runs.IsRunActive)
            {
                throw new InvalidOperationException("The turn is still running, so it cannot be retried. Wait for it to finish.");
            }

            await TurnOnUiThreadAsync(sessionId, PromptOf(sessionId), cancellationToken).ConfigureAwait(true);
        });

    /// <summary>Steers the running turn through the native steering channel after setting the local origin.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="text">The steer text.</param>
    /// <param name="arguments">Model-supplied arguments.</param>
    /// <param name="cancellationToken">Cancels the steer.</param>
    /// <returns>The task.</returns>
    public Task SteerAsync(string sessionId, string text, JsonElement? arguments, CancellationToken cancellationToken) =>
        OnUiThreadAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(text);
            cancellationToken.ThrowIfCancellationRequested();
            SetOrigin(sessionId);
            await SendOrSteerAsync(SlotOf(sessionId), text, cancellationToken).ConfigureAwait(true);
        });

    /// <summary>Stops engines after confirmation. Rows stay in the registry.</summary>
    /// <param name="confirmed">Whether the user confirmed sign-out.</param>
    /// <returns>The task.</returns>
    public async Task SignOutAsync(bool confirmed)
    {
        if (!confirmed)
        {
            return;
        }

        var completion = QueueOperation(out var previous);
        try
        {
            await previous.ConfigureAwait(false);
            await StopSessionsAsync().ConfigureAwait(false);
        }
        finally
        {
            completion.SetResult();
        }
    }

    /// <summary>Checks image attachments. A model that reads none is refused. Over-limit images leave the prompt unsent.</summary>
    /// <param name="supportsImages">Whether the model reads images.</param>
    /// <param name="images">The images.</param>
    /// <returns>The attachment result.</returns>
    public static CoworkAttachmentResult AttachImages(bool? supportsImages, IReadOnlyList<AcpPromptImage> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        if (supportsImages == false && images.Count > 0)
        {
            return new CoworkAttachmentResult(false, AttachmentCopy.ModelRefuses, null);
        }

        var blocks = AcpImageContent.Build(images);
        var note = blocks.LastOrDefault(static block => string.Equals(block.Type, "text", StringComparison.Ordinal));
        if (note is not null && images.Count > 0 && blocks.Count(static block => string.Equals(block.Type, "image", StringComparison.Ordinal)) != images.Count)
        {
            return new CoworkAttachmentResult(false, note.Text, note.Text);
        }

        return new CoworkAttachmentResult(true, null, null);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposeTask is null)
            {
                _disposed = true;
                _disposeTask = DisposeCoreAsync(_operations);
            }

            return new ValueTask(_disposeTask);
        }
    }

    private TaskCompletionSource QueueOperation(out Task previous)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Enqueue(out previous);
        }
    }

    private TaskCompletionSource? TryQueueOperation(out Task previous)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                previous = Task.CompletedTask;
                return null;
            }

            return Enqueue(out previous);
        }
    }

    // Requires _gate. The new tail completes when the caller calls SetResult.
    private TaskCompletionSource Enqueue(out Task previous)
    {
        previous = _operations;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _operations = completion.Task;
        return completion;
    }

    private async Task DisposeCoreAsync(Task operations)
    {
        // Tools is borrowed from CoworkHead, which closes it only after every acquisition and engine has stopped.
        await operations.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        await StopSessionsAsync().ConfigureAwait(false);
    }

    private Task StopSessionsAsync()
    {
        Slot[] slots;
        lock (_gate)
        {
            slots = [.. _slots.Values];
            _slots.Clear();
        }

        return DisposeSlotsAsync(slots);
    }

    private Task TurnAsync(string sessionId, string prompt, CancellationToken cancellationToken) =>
        OnUiThreadAsync(() => TurnOnUiThreadAsync(sessionId, prompt, cancellationToken));

    private async Task TurnOnUiThreadAsync(string sessionId, string prompt, CancellationToken cancellationToken)
    {
        SetOrigin(sessionId);
        var slot = SlotOf(sessionId);
        slot.LastPrompt = prompt;
        cancellationToken.ThrowIfCancellationRequested();
        await SendOrSteerAsync(slot, prompt, cancellationToken).ConfigureAwait(true);
    }

    private async Task DisposeSlotsAsync(Slot[] slots)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        foreach (var slot in slots)
        {
            try
            {
                await slot.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                _options.Tools?.CloseSession(slot.Runtime.ViewModel.Id);
            }
        }

        Volatile.Write(ref _enginesStopped, 1);
        failure?.Throw();
    }

    private void SetOrigin(string sessionId) =>
        SlotOf(sessionId).Lease.SetTurnOrigin(CoworkTurnOrigin.Local);

    private string PromptOf(string sessionId) => SlotOf(sessionId).LastPrompt ?? string.Empty;

    private Slot SlotOf(string sessionId)
    {
        lock (_gate)
        {
            return _slots[sessionId];
        }
    }

    private CoworkSessionRegistry EnsureRegistry(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_registry is null)
            {
                var registry = CoworkSessionRegistry.Open(
                    new CoworkSessionOptions { StateRoot = _options.StateRoot },
                    _options.Gate,
                    _options.Clock,
                    _options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Cowork"));
                _ = registry.RecoverAfterRestart();
                Recoveries++;
                _registry = registry;
            }

            return _registry;
        }
    }

    [SuppressMessage("Maintainability", "MA0051", Justification = "The acquire, transfer and cleanup steps stay in one flow so the cleanup guarantee sits beside the owners it protects.")]
    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: each owner is nulled once transferred to its composed holder; the finally disposes any owner still held directly.")]
    private async Task<Slot> OpenCoreAsync(AgentRow row, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_options.StateRoot, "cowork-sessions", row.Id);
        HostLease? lease = null;
        EngineComposition? engine = null;
        AgentSessionRuntime? runtime = null;
        var completed = false;
        try
        {
            lease = new HostLease();
            engine = await ComposeEngineAsync(row, directory, lease, cancellationToken).ConfigureAwait(false);
            runtime = new AgentSessionRuntime(RuntimeOptions(row, directory, engine));
            cancellationToken.ThrowIfCancellationRequested();
            var slot = new Slot(runtime, lease, engine);
            runtime = null;
            engine = null;
            lease = null;
            completed = true;
            return slot;
        }
        finally
        {
            ExceptionDispatchInfo? failure = null;
            try
            {
                if (runtime is not null)
                {
                    await runtime.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                if (engine is not null)
                {
                    await engine.Host.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                if (lease is not null)
                {
                    await lease.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                if (lease is not null)
                {
                    _options.Tools?.CloseSession(row.Id);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            CleanupFailureReporter.Report(failure, completed, _options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Cowork"));
        }
    }

    private async Task<EngineComposition> ComposeEngineAsync(AgentRow row, string directory, HostLease lease, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        string? notice = null;
        CoworkGatewayEntry? gateway = null;
        if (_options.Tools is not null)
        {
            var opened = await _options.Tools.OpenAsync(row.Id, cancellationToken).ConfigureAwait(false);
            gateway = opened.Entry;
            notice = opened.Notice;
        }

        EngineComposition? engine = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var boundary = new CoworkPathBoundary(_options.StateRoot);
            var composed = CoworkEngineComposer.Compose(new CoworkEngineRequest(directory, _options.WorkingDirectory, [_options.WorkingDirectory], boundary, () => lease.IsTainted)
            {
                ProviderKeys = _options.ProviderKeys,
                Gateway = gateway,
                Catalog = _options.Catalog,
                Remote = _options.Remote,
                TransportFactory = _options.TransportFactory,
                LoggerFactory = _options.LoggerFactory,
                OmpPath = _options.OmpPath,
                ResumeSessionId = row.Summary.GraphId,
            });
            engine = composed.Engine;
            var notices = new List<string>(composed.Notices);
            if (notice is not null)
            {
                notices.Add(notice);
            }
            Notices = notices;
            var result = engine;
            engine = null;
            return result;
        }
        finally
        {
            if (engine is not null)
            {
                await engine.Host.DisposeAsync().ConfigureAwait(false);
                engine = null;
            }
        }
    }


    private AgentSessionRuntimeOptions RuntimeOptions(AgentRow row, string directory, EngineComposition engine) => new()
    {
        Row = row,
        Prompts = new CoworkPromptChannel(engine),
        Steering = null, // Cowork steers exclusively through the native LiveSteering channel (set below as NativeSteering); the ISteeringChannel path is for terminals and fallback when omp does not support live steering. Unconditional like NativeSteering to match Code parity at SessionEngineSet:287 and ensure steer text reaches runtime.
        NativeSteering = engine.Host.LiveSteering,
        Approvals = new CoworkApprovalBridge(Broker),
        Scope = new StageObservationScope { SessionId = row.Id, WorkspaceRoot = _options.WorkingDirectory, SessionStateDirectory = directory },
        Stages = new IdleStages(),
        Context = new IdleContext(),
        TimeProvider = _options.Clock,
    };
    // The runtime and its run input are UI-affine: the held-message queue has no lock, and change notifications
    // reach the views on the UI thread. Every send, steer and retry therefore runs there, whichever thread its caller is on.
    private static Task OnUiThreadAsync(Func<Task> work) => Dispatcher.UIThread.InvokeAsync(work);

    // A message sent while a run is active steers that run. Otherwise the runtime's own hand-over takes it: it starts a turn, or queues it
    // behind a run that began meanwhile, as the chat composer does. Runs on the UI thread, so each await resumes there.
    private static async Task SendOrSteerAsync(Slot slot, string text, CancellationToken cancellationToken)
    {
        if (slot.Runtime.Runs.IsRunActive)
        {
            var answer = await slot.Engine.Host.LiveSteering.SteerAsync(text, OmpSteerLane.Steering, cancellationToken).ConfigureAwait(true);
            switch (answer.Outcome)
            {
                case OmpSteerOutcome.Injected:
                    return;
                case OmpSteerOutcome.PromptRequired:
                case OmpSteerOutcome.NotRunning:
                    // The run ended while the message was in flight, so the hand-over below takes the message.
                    break;
                case OmpSteerOutcome.Unsupported:
                    throw new InvalidOperationException("This omp cannot take a message into a running turn.");
                default:
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(answer.Reason) ? "omp could not take the message." : answer.Reason);
            }
        }

        var delivery = await slot.Runtime.SubmitAsNewTurnAsync(text, cancellationToken).ConfigureAwait(true);
        if (delivery.IsFailure)
        {
            throw new InvalidOperationException(delivery.Reason ?? "The message was not sent.");
        }
    }

    // The send path of a Cowork session: it forwards to the engine the slot composed, as the Code surface's slot channel does.
    private sealed class CoworkPromptChannel(EngineComposition engine) : IPromptChannel
    {
        public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, CancellationToken cancellationToken) =>
            SubmitAsync(prompt, new PromptOptions(), cancellationToken);

        public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, PromptOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            ArgumentNullException.ThrowIfNull(options);
            IEngineHost host = engine.Reconnecting is { } reconnecting ? reconnecting : engine.Host;
            return host.SubmitPromptAsync(prompt, options, cancellationToken);
        }

        public Task CancelAsync(CancellationToken cancellationToken) =>
            engine.Reconnecting?.CancelAsync(cancellationToken) ?? engine.Host.CancelAsync(cancellationToken);

        // The slot owns the engine, so closing the send path of a runtime stops nothing.
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Slot(AgentSessionRuntime runtime, HostLease lease, EngineComposition engine) : IAsyncDisposable
    {
        public AgentSessionRuntime Runtime { get; } = runtime;

        public HostLease Lease { get; } = lease;

        public EngineComposition Engine { get; } = engine;

        public string? LastPrompt { get; set; }

        public ValueTask DisposeAsync() => DisposeSessionAsync(Runtime, Engine, Lease);
    }

    private static async ValueTask DisposeSessionAsync(AgentSessionRuntime? runtime, EngineComposition? engine, HostLease lease)
    {
        try
        {
            if (runtime is not null)
            {
                await runtime.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            try
            {
                if (engine is not null)
                {
                    await engine.Host.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private sealed class HostLease : ICoworkSessionLease
    {
        public List<CoworkTurnOrigin> Origins { get; } = [];

        public bool IsTainted { get; private set; }

        public void SetTurnOrigin(CoworkTurnOrigin origin) => Origins.Add(origin);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class IdleStages : IStageSource
    {
        public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _ = scope;
            await Task.CompletedTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            yield break;
        }
    }

    private sealed class IdleContext : ITaskContextSource
    {
        public Task<TaskContext> GetAsync(StageObservationScope scope, CancellationToken cancellationToken) =>
            Task.FromResult(new TaskContext { Title = string.Empty, SharedMemory = [], Lessons = [], Warnings = [] });
    }
}
