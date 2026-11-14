using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Chat;
using Autonogrammer.Cowork.Instructions;
using Autonogrammer.Cowork.Sessions;
using Autonogrammer.EngineHost;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Logging;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Composer.Attachments;
using Autonogrammer.Presentation.Cowork.Instructions;
using Avalonia.Threading;

namespace Autonogrammer.Native.Cowork;

/// <summary>The Cowork <see cref="IAgentPanelHost"/>: one registry and engine per opened task, local turn origin on every prompt. Startup, close and sign-out are serialized; disposal drains their cleanup before the head closes its borrowed tool server.</summary>
internal sealed class CoworkSessionHost : IAgentPanelHost, IAsyncDisposable
{
    private readonly CoworkSessionHostOptions _options;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Slot> _slots = new(StringComparer.Ordinal);
    private CoworkSessionRegistry? _registry;
    private string? _registryAccountKey;
    // The tail always completes successfully, even when an operation fails. Shutdown drains it before closing tools.
    private Task _operations = Task.CompletedTask;
    private Task? _disposeTask;
    private int _enginesStopped;
    private bool _disposed;

    /// <summary>Creates the host. It opens the registry on the first create or reload.</summary>
    /// <param name="options">The seams.</param>
    /// <param name="broker">The approval broker the conversation cards read. The desktop head passes the one it shares with the Computer Use provider; null builds one from the options.</param>
    public CoworkSessionHost(CoworkSessionHostOptions options, CoworkApprovalBroker? broker = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        Broker = broker ?? new CoworkApprovalBroker(options.ToolOptions, options.Clock, options.WindowAvailable);
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

    // The memory mode a linked task is created with. Memory then follows the account's settings; the task's own flag is stored and read back as set.
    private const bool LinkedTaskMemoryOff = false;

    // The first-prompt sentences. Each says what was not sent and what the user can do; none quotes the task context.
    private const string FirstPromptUnconfirmedMessage = "The task context may not have reached omp, so this message was not sent. Resend the task context or start without it first.";
    private const string FirstPromptSlashMessage = "The first message of a Cowork task cannot start with '/'. The task context goes before the message, and omp runs a slash command only when it is the first text. Send a message that does not start with '/'.";
    private const string FirstPromptUnconfirmedNotice = "The task context may not have reached omp. Sending is paused until you resend it or start without it.";

    /// <summary>
    /// Creates the task the rail asked for. No project creates an unlinked task, exactly as <see cref="CreateSessionAsync"/> does. A project creates a task
    /// linked to it: the project, its trusted folders and the task's memory mode are stored before the identifier is returned. Nothing is opened.
    /// </summary>
    /// <param name="projectId">The project, or null for an unlinked task.</param>
    /// <param name="cancellationToken">Cancels the create.</param>
    /// <returns>
    /// The task identifier, and a sentence to show, or null. The sentence says a project folder is missing on this device; the task is still created.
    /// </returns>
    /// <exception cref="InvalidOperationException">Signed out, or the project is not an active project of the signed-in account.</exception>
    public async Task<(string Id, string? Notice)> CreateTaskAsync(string? projectId, CancellationToken cancellationToken)
    {
        if (projectId is null)
        {
            var unlinked = await CreateSessionAsync(cancellationToken).ConfigureAwait(false);
            return (unlinked, null);
        }

        return CreateLinkedTask(projectId, cancellationToken);
    }

    /// <summary>Returns the account's tasks in registry order, each with the project it is linked to. Signed out, there are none.</summary>
    /// <returns>A snapshot that later changes do not affect.</returns>
    public IReadOnlyList<Contracts.Cowork.CoworkSessionSummary> TaskSummaries()
    {
        RetireStaleSessions();
        var accountKey = SignedInKeyOrNull();
        if (accountKey is null)
        {
            return [];
        }

        var rows = EnsureRegistry(CancellationToken.None).List();
        var meta = new CoworkSessionMetaStore(_options.StateRoot, accountKey);
        return [.. rows.Select(row => new Contracts.Cowork.CoworkSessionSummary
        {
            Id = row.Id,
            Title = row.Title,
            Status = row.Status,
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch,
            ProjectId = ProjectIdOf(meta, row.Id),
        })];
    }

    /// <summary>
    /// Reads what a task takes at its start now: its project, the project's instructions, the global instructions and the memory brief.
    /// The project comes from the task's stored link, so reopening a task reads its own project, not the one selected in the rail.
    /// </summary>
    /// <param name="sessionId">The task.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The account snapshot, or null when the head has no account data.</returns>
    public async Task<CoworkAccountSnapshot?> ResolveTaskContextAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (_options.AccountContext is null)
        {
            return null;
        }

        return await _options.AccountContext.SnapshotAsync(sessionId, cancellationToken).ConfigureAwait(false);
    }

    private (string Id, string? Notice) CreateLinkedTask(string projectId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        cancellationToken.ThrowIfCancellationRequested();
        var accountKey = SignedInKeyOrNull() ?? throw new InvalidOperationException("Sign in to create a Cowork task.");
        // The project, its folders and the mode are read from the account's local copy before anything is written.
        var project = ActiveProjectOf(accountKey, projectId);
        var meta = new CoworkSessionMeta
        {
            ProjectId = project.Id,
            TrustedFolders = TrustedFoldersOf(accountKey, project),
            MemoryOff = LinkedTaskMemoryOff,
        };
        var notice = CoworkDegradedSentences.TryFor(Autonogrammer.Cowork.Projects.CoworkProjectStore.Workspace(project).Notice);
        var registry = EnsureRegistry(cancellationToken);
        var id = registry.Create(null).Id;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            new CoworkSessionMetaStore(_options.StateRoot, accountKey).Upsert(id, meta);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A task whose link was not stored must not open as an unlinked task. It is marked Failed; the original error keeps propagating.
            MarkFailed(registry, id);
            throw;
        }

        return (id, notice);
    }

    private void MarkFailed(CoworkSessionRegistry registry, string id)
    {
        ExceptionDispatchInfo? failure = null;
        try
        {
            _ = registry.SetStatus(id, Contracts.Cowork.CoworkSessionStatus.Failed);
        }
        catch (Exception cleanup) when (cleanup is not OutOfMemoryException)
        {
            failure = ExceptionDispatchInfo.Capture(cleanup);
        }

        CleanupFailureReporter.Report(failure, completed: false, _options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Cowork"));
    }

    /// <inheritdoc />
    public Task<string> CreateTerminalSessionAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Cowork has no terminal sessions.");

    /// <inheritdoc />
    public async Task<AgentSessionViewModel> OpenSessionAsync(AgentRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        await RetireStaleSessionsAsync().ConfigureAwait(false);
        var completion = QueueOperation(out var previous);
        Slot? slot = null;
        try
        {
            await previous.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (OpenedViewModelOrNull(row.Id) is { } opened)
            {
                return opened;
            }

            // slot is owned here until it is stored in _slots; the finally disposes it when it never was.
            slot = await OpenCoreAsync(row, SignedInKeyOrNull(), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var viewModel = slot.Runtime.ViewModel;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                ThrowIfAccountChanged(slot);
                _slots.Add(row.Id, slot);
                slot = null;
                Volatile.Write(ref _enginesStopped, 0);
            }

            return viewModel;
        }
        finally
        {
            ExceptionDispatchInfo? failure = null;
            if (slot is not null)
            {
                try
                {
                    await slot.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    failure = ExceptionDispatchInfo.Capture(exception);
                }

                try
                {
                    _options.Tools?.CloseSession(row.Id);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    failure ??= ExceptionDispatchInfo.Capture(exception);
                }
            }

            completion.SetResult();
            CleanupFailureReporter.Report(failure, completed: false, _options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Cowork"));
        }
    }

    // The view model of a session already open, or null. A disposed host refuses.
    private AgentSessionViewModel? OpenedViewModelOrNull(string sessionId)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _slots.TryGetValue(sessionId, out var existing) ? existing.Runtime.ViewModel : null;
        }
    }

    // A slot whose account is no longer signed in is refused. The caller still owns it and disposes it.
    private void ThrowIfAccountChanged(Slot slot)
    {
        if (!string.Equals(SignedInKeyOrNull(), slot.AccountKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The account changed while the session was opening, so the session was not opened.");
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
        await RetireStaleSessionsAsync().ConfigureAwait(false);
        _ = EnsureRegistry(cancellationToken);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the task's standing block again with its next message. The user chooses this after an unknown outcome, so omp may end up holding the block twice.
    /// </summary>
    /// <param name="sessionId">The task.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The task.</returns>
    public async Task ResendFirstPromptAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();
        FirstPromptStoreOrThrow().Resend(sessionId);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>Starts the task without its standing block after an unknown outcome. The block is not sent again.</summary>
    /// <param name="sessionId">The task.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The task.</returns>
    public async Task AbandonFirstPromptAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();
        FirstPromptStoreOrThrow().Abandon(sessionId);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private CoworkFirstPromptStore FirstPromptStoreOrThrow()
    {
        var accountKey = SignedInKeyOrNull() ?? throw new InvalidOperationException("Sign in to change the task context.");
        return new CoworkFirstPromptStore(_options.StateRoot, accountKey);
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
            if (LiveSlotOf(sessionId).Runtime.Runs.IsRunActive)
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
            await SendOrSteerAsync(LiveSlotOf(sessionId), text, cancellationToken).ConfigureAwait(true);
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

    private async Task StopSessionsAsync()
    {
        Slot[] slots;
        lock (_gate)
        {
            slots = [.. _slots.Values];
            _slots.Clear();
        }

        try
        {
            await DisposeSlotsAsync(slots).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _enginesStopped, 1);
        }
    }

    // Stops the open sessions whose account is no longer the signed-in one. The stop is queued behind the operations already running, so it never races a start.
    private void RetireStaleSessions()
    {
        if (HasStaleSessions())
        {
            _ = RetireStaleSessionsAsync();
        }
    }

    private bool HasStaleSessions()
    {
        lock (_gate)
        {
            var current = SignedInKeyOrNull();
            return _slots.Values.Any(slot => !string.Equals(slot.AccountKey, current, StringComparison.Ordinal));
        }
    }

    private async Task RetireStaleSessionsAsync()
    {
        var completion = TryQueueOperation(out var previous);
        if (completion is null)
        {
            return;
        }

        ExceptionDispatchInfo? failure = null;
        var completed = false;
        try
        {
            await previous.ConfigureAwait(false);
            List<Slot> stale = [];
            lock (_gate)
            {
                var current = SignedInKeyOrNull();
                foreach (var (key, slot) in _slots.Where(pair => !string.Equals(pair.Value.AccountKey, current, StringComparison.Ordinal)).ToArray())
                {
                    _ = _slots.Remove(key);
                    stale.Add(slot);
                }
            }

            await DisposeSlotsAsync([.. stale]).ConfigureAwait(false);
            completed = true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            completion.SetResult();
        }

        CleanupFailureReporter.Report(failure, completed, _options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Cowork"));
    }

    // The slot of a session whose account is still the signed-in one. A session of a retired account is refused, and its teardown is scheduled.
    private Slot LiveSlotOf(string sessionId)
    {
        var slot = SlotOf(sessionId);
        if (string.Equals(slot.AccountKey, SignedInKeyOrNull(), StringComparison.Ordinal))
        {
            return slot;
        }

        RetireStaleSessions();
        throw new InvalidOperationException("The account of this session is no longer signed in, so the session was stopped.");
    }

    private Task TurnAsync(string sessionId, string prompt, CancellationToken cancellationToken) =>
        OnUiThreadAsync(() => TurnOnUiThreadAsync(sessionId, prompt, cancellationToken));

    private async Task TurnOnUiThreadAsync(string sessionId, string prompt, CancellationToken cancellationToken)
    {
        SetOrigin(sessionId);
        var slot = LiveSlotOf(sessionId);
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

        failure?.Throw();
    }

    private void SetOrigin(string sessionId) =>
        LiveSlotOf(sessionId).Lease.SetTurnOrigin(CoworkTurnOrigin.Local);

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
            var accountKey = SignedInKeyOrNull();
            if (_registry is not null && !string.Equals(_registryAccountKey, accountKey, StringComparison.Ordinal))
            {
                // The account changed. The registry of the previous account is never used for the new one.
                _registry = null;
                _registryAccountKey = null;
            }

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
                _registryAccountKey = accountKey;
            }

            return _registry;
        }
    }

    private string? SignedInKeyOrNull()
    {
        var key = _options.Gate.IsSignedIn ? _options.Gate.AccountFolderKey : null;
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }

    private Autonogrammer.Cowork.Projects.CoworkProject ActiveProjectOf(string accountKey, string projectId)
    {
        var project = new Autonogrammer.Cowork.Projects.CoworkProjectStore(_options.StateRoot, accountKey).List(includeArchived: true)
            .FirstOrDefault(candidate => string.Equals(candidate.Id, projectId, StringComparison.Ordinal));
        if (project is null)
        {
            throw new InvalidOperationException("The project is not on this device for the signed-in account. The task was not created.");
        }

        if (project.ArchivedAt is not null)
        {
            throw new InvalidOperationException("The project is archived. The task was not created.");
        }

        return project;
    }

    // The folders a linked task trusts: the project's folders that exist on this device, then the device's folder grants. Paths stay local.
    private List<string> TrustedFoldersOf(string accountKey, Autonogrammer.Cowork.Projects.CoworkProject project)
    {
        var folders = new List<string>();
        foreach (var folder in project.Folders)
        {
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder) && !folders.Contains(folder, StringComparer.Ordinal))
            {
                folders.Add(folder);
            }
        }

        foreach (var grant in new CoworkFolderGrantStore(_options.StateRoot, accountKey).List())
        {
            if (!string.IsNullOrWhiteSpace(grant.Path) && !folders.Contains(grant.Path, StringComparer.Ordinal))
            {
                folders.Add(grant.Path);
            }
        }

        return folders;
    }

    private static string? ProjectIdOf(CoworkSessionMetaStore meta, string sessionId)
    {
        try
        {
            return meta.Get(sessionId)?.ProjectId;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    [SuppressMessage("Maintainability", "MA0051", Justification = "The acquire, transfer and cleanup steps stay in one flow so the cleanup guarantee sits beside the owners it protects.")]
    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: each owner is nulled once transferred to its composed holder; the finally disposes any owner still held directly.")]
    private async Task<Slot> OpenCoreAsync(AgentRow row, string? accountKey, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_options.StateRoot, "cowork-sessions", row.Id);
        HostLease? lease = null;
        EngineComposition? engine = null;
        AgentSessionRuntime? runtime = null;
        var completed = false;
        try
        {
            lease = new HostLease();
            var (composed, firstPrompt) = await ComposeEngineAsync(row, directory, lease, accountKey, cancellationToken).ConfigureAwait(false);
            engine = composed.Engine;
            runtime = new AgentSessionRuntime(RuntimeOptions(row, directory, engine, firstPrompt));
            cancellationToken.ThrowIfCancellationRequested();
            var slot = new Slot(runtime, lease, engine, accountKey);
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

    private async Task<(CoworkEngineComposition Composed, FirstPromptSend? FirstPrompt)> ComposeEngineAsync(AgentRow row, string directory, HostLease lease, string? accountKey, CancellationToken cancellationToken)
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

        var start = await StartContextAsync(row.Id, accountKey, cancellationToken).ConfigureAwait(false);
        var firstPrompt = FirstPromptOf(row.Id, accountKey, start.Standing);
        CoworkEngineComposition? composed = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var boundary = new CoworkPathBoundary(_options.StateRoot);
            var request = new CoworkEngineRequest(directory, _options.WorkingDirectory, SessionTrustedFolders(row.Id, accountKey), boundary, () => lease.IsTainted)
            {
                SessionId = row.Id,
                Standing = start.Standing,
                ProviderKeys = _options.ProviderKeys,
                Gateway = gateway,
                Catalog = _options.Catalog,
                Remote = _options.Remote,
                TransportFactory = _options.TransportFactory,
                LoggerFactory = _options.LoggerFactory,
                OmpPath = _options.OmpPath,
                ResumeSessionId = row.Summary.GraphId,
                LoadReplaySink = firstPrompt?.ReplaySink,
            };
            composed = CoworkEngineComposer.Compose(request);
            var notices = new List<string>(composed.Notices);
            notices.AddRange(start.Notices);
            if (firstPrompt?.Notice is { } firstPromptNotice)
            {
                notices.Add(firstPromptNotice);
            }

            if (notice is not null)
            {
                notices.Add(notice);
            }

            Notices = notices;
            var result = composed;
            composed = null;
            return (result, firstPrompt);
        }
        finally
        {
            if (composed is not null)
            {
                await composed.Engine.Host.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    // The task's standing block, taken at its first start and kept: a restart before the first send reuses it. Null when the task has none to take.
    private FirstPromptSend? FirstPromptOf(string sessionId, string? accountKey, StandingContext? standing)
    {
        if (accountKey is null)
        {
            return null;
        }

        var store = new CoworkFirstPromptStore(_options.StateRoot, accountKey);
        var record = store.Ensure(sessionId, standing?.Text);
        return record is null ? null : new FirstPromptSend(store, sessionId, record.State);
    }

    // The session's standing context, read now from the account it belongs to. A snapshot of another account, or one read after a switch, refuses the start.
    private async Task<(StandingContext? Standing, IReadOnlyList<string> Notices)> StartContextAsync(string sessionId, string? accountKey, CancellationToken cancellationToken)
    {
        var account = _options.AccountContext;
        if (account is null)
        {
            return (null, Array.Empty<string>());
        }

        var snapshot = await account.SnapshotAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (!account.IsCurrent(snapshot) || !string.Equals(SignedInKeyOrNull(), accountKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The account changed while the session was opening, so the session was not opened.");
        }

        var context = StandingContextBuilder.Build(snapshot.GlobalInstructions, snapshot.ProjectInstructions, null, snapshot.MemoryBrief);
        return (context, NoticesFor(context.Notices));
    }

    // Shown without the text that was cut: the notice says that something was shortened or left out, never what it said.
    private static List<string> NoticesFor(IReadOnlyList<string> codes)
    {
        var notices = new List<string>(codes.Count);
        foreach (var code in codes)
        {
            notices.Add(string.Equals(code, StandingContextBuilder.StandingContextOverBudget, StringComparison.Ordinal)
                ? "The memory notes were left out of this session because the standing context is over its budget."
                : "Part of the standing context was shortened to fit this session.");
        }

        return notices;
    }

    // The folders a session trusts: its working directory, then the folders its record stored when it was linked to a project. Paths stay local.
    private List<string> SessionTrustedFolders(string sessionId, string? accountKey)
    {
        var folders = new List<string> { _options.WorkingDirectory };
        if (accountKey is null)
        {
            return folders;
        }

        foreach (var folder in ReadMeta(accountKey, sessionId)?.TrustedFolders ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(folder) && !folders.Contains(folder, StringComparer.Ordinal))
            {
                folders.Add(folder);
            }
        }

        return folders;
    }

    private CoworkSessionMeta? ReadMeta(string accountKey, string sessionId)
    {
        try
        {
            return new CoworkSessionMetaStore(_options.StateRoot, accountKey).Get(sessionId);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }


    private AgentSessionRuntimeOptions RuntimeOptions(AgentRow row, string directory, EngineComposition engine, FirstPromptSend? firstPrompt) => new()
    {
        Row = row,
        Prompts = new CoworkPromptChannel(engine, firstPrompt),
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

    // The send path of a Cowork session: it forwards to the engine the slot composed. A task's first send goes through its first-prompt path first.
    private sealed class CoworkPromptChannel(EngineComposition engine, FirstPromptSend? firstPrompt) : IPromptChannel
    {
        public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, CancellationToken cancellationToken) =>
            SubmitAsync(prompt, new PromptOptions(), cancellationToken);

        public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, PromptOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            ArgumentNullException.ThrowIfNull(options);
            IEngineHost host = engine.Reconnecting is { } reconnecting ? reconnecting : engine.Host;
            return firstPrompt is null
                ? host.SubmitPromptAsync(prompt, options, cancellationToken)
                : firstPrompt.SubmitAsync(host, prompt, options, cancellationToken);
        }

        public Task CancelAsync(CancellationToken cancellationToken) =>
            engine.Reconnecting?.CancelAsync(cancellationToken) ?? engine.Host.CancelAsync(cancellationToken);

        // The slot owns the engine, so closing the send path of a runtime stops nothing.
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // One task's first-send path. A pending block goes out in front of the message, once. Its outcome is recorded: acknowledged only when the transmitted
    // request completed; an unknown outcome stops sends until the user resends or abandons the block. Nothing is written here that the block text could leak into.
    private sealed class FirstPromptSend
    {
        private readonly CoworkFirstPromptStore _store;
        private readonly string _sessionId;

        public FirstPromptSend(CoworkFirstPromptStore store, string sessionId, CoworkFirstPromptState state)
        {
            _store = store;
            _sessionId = sessionId;
            ReplaySink = new FirstPromptReplay(store, sessionId);
            Notice = state is CoworkFirstPromptState.InFlight or CoworkFirstPromptState.Unconfirmed ? FirstPromptUnconfirmedNotice : null;
        }

        /// <summary>Gets the sink that records the block when omp replays it on load.</summary>
        public IAcpLoadReplaySink ReplaySink { get; }

        /// <summary>Gets the sentence to show when the task opens with an unknown outcome, or null.</summary>
        public string? Notice { get; }

        public async IAsyncEnumerable<HarnessEvent> SubmitAsync(
            IEngineHost host,
            string prompt,
            PromptOptions options,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var record = _store.Read(_sessionId);
            if (record is null || record.State is CoworkFirstPromptState.Acknowledged or CoworkFirstPromptState.Abandoned)
            {
                await foreach (var passed in host.SubmitPromptAsync(prompt, options, cancellationToken).ConfigureAwait(false))
                {
                    yield return passed;
                }

                yield break;
            }

            if (record.State is not CoworkFirstPromptState.Pending)
            {
                throw new InvalidOperationException(FirstPromptUnconfirmedMessage);
            }

            // omp runs a leading slash command from the first text block, and the block must come before the text: the two cannot both hold.
            if (prompt.TrimStart().StartsWith('/'))
            {
                throw new InvalidOperationException(FirstPromptSlashMessage);
            }

            var transmission = new Transmission();
            var completed = false;
            try
            {
                _store.MarkInFlight(_sessionId);
                var carried = options with
                {
                    Resources = [StandingResource(record), .. options.Resources],
                    RequireEmbeddedContext = true,
                    Transmitted = transmission.Mark,
                };
                await foreach (var harnessEvent in host.SubmitPromptAsync(prompt, carried, cancellationToken).ConfigureAwait(false))
                {
                    completed |= harnessEvent is CompletedEvent;
                    yield return harnessEvent;
                }
            }
            finally
            {
                _store.Settle(_sessionId, transmission.Sent, completed);
            }
        }

        private static PromptResource StandingResource(CoworkFirstPromptRecord record) => new(
            record.Uri,
            record.MimeType,
            record.Text,
            "Task context",
            System.Text.Encoding.UTF8.GetByteCount(record.Text),
            false);
    }

    // Records the block omp replays when the task is loaded. A replayed user message that carries the block acknowledges it.
    private sealed class FirstPromptReplay(CoworkFirstPromptStore store, string sessionId) : IAcpLoadReplaySink
    {
        public void LoadReplayed(AcpLoadReplay replay)
        {
            ArgumentNullException.ThrowIfNull(replay);
            var uri = CoworkFirstPromptStore.ResourceUriFor(sessionId);
            foreach (var update in replay.Updates)
            {
                if (ReplayedBlockOf(update, uri) is { } text)
                {
                    store.RecordReplay(sessionId, uri, text);
                }
            }
        }

        // The text of a replayed user message that carries the block, exactly as it arrived. Anything else is not the block.
        private static string? ReplayedBlockOf(JsonElement update, Uri uri)
        {
            if (StringOf(update, "sessionUpdate") is not "user_message_chunk")
            {
                return null;
            }

            var content = ObjectOf(update, "content");
            if (StringOf(content, "type") is not "resource")
            {
                return null;
            }

            var resource = ObjectOf(content, "resource");
            return string.Equals(StringOf(resource, "uri"), uri.AbsoluteUri, StringComparison.Ordinal) ? StringOf(resource, "text") : null;
        }

        private static JsonElement ObjectOf(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

        private static string? StringOf(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    // Set by the transport once the prompt request is written. Read after the turn ends, possibly from another thread.
    private sealed class Transmission
    {
        private int _sent;

        public bool Sent => Volatile.Read(ref _sent) != 0;

        public void Mark()
        {
            _ = Interlocked.Exchange(ref _sent, 1);
        }
    }

    private sealed class Slot(AgentSessionRuntime runtime, HostLease lease, EngineComposition engine, string? accountKey) : IAsyncDisposable
    {
        public AgentSessionRuntime Runtime { get; } = runtime;

        public HostLease Lease { get; } = lease;

        public EngineComposition Engine { get; } = engine;

        public string? AccountKey { get; } = accountKey;

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
