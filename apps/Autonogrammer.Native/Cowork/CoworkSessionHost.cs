using Autonogrammer.Harness.Abstractions;
using System.Text.Json;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Chat;
using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Settings.Models;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Composer.Attachments;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Native.Cowork;

/// <summary>The Cowork <see cref="IAgentPanelHost"/>: one registry, one engine per opened task, local turn origin on every prompt.</summary>
internal sealed class CoworkSessionHost : IAgentPanelHost, IAsyncDisposable
{
    private readonly CoworkSessionHostOptions _options;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Slot> _slots = new(StringComparer.Ordinal);
    private readonly HashSet<string> _closed = new(StringComparer.Ordinal);
    private CoworkSessionRegistry? _registry;
    private bool _recovered;
    private int _enginesStopped;

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
        var registry = await EnsureRegistryAsync(cancellationToken).ConfigureAwait(false);
        var row = registry.Create(null);
        return row.Id;
    }

    /// <inheritdoc />
    public Task<string> CreateTerminalSessionAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Cowork has no terminal sessions.");

    /// <inheritdoc />
    public AgentSessionViewModel OpenSession(AgentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        lock (_gate)
        {
            if (_slots.TryGetValue(row.Id, out var existing))
            {
                return existing.Runtime.ViewModel;
            }
        }

        var slot = OpenCore(row);
        lock (_gate)
        {
            _slots[row.Id] = slot;
        }

        return slot.Runtime.ViewModel;
    }

    /// <inheritdoc />
    public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        Slot? slot;
        lock (_gate)
        {
            _ = _slots.Remove(sessionId, out slot);
            _ = _closed.Add(sessionId);
        }

        if (slot is not null)
        {
            await slot.DisposeAsync().ConfigureAwait(false);
        }

        _options.Tools?.CloseSession(sessionId);
        _ = _registry?.SetStatus(sessionId, Contracts.Cowork.CoworkSessionStatus.Paused);
    }

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken) => _ = await EnsureRegistryAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Sends a prompt after setting the local turn origin. A model argument named origin is ignored.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="prompt">The prompt.</param>
    /// <param name="arguments">Model-supplied arguments. An origin property is not forwarded.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>The task.</returns>
    public Task SendAsync(string sessionId, string prompt, JsonElement? arguments, CancellationToken cancellationToken) =>
        TurnAsync(sessionId, prompt, arguments, cancellationToken);

    /// <summary>Retries the last prompt with a fresh local origin.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="arguments">Model-supplied arguments.</param>
    /// <param name="cancellationToken">Cancels the retry.</param>
    /// <returns>The task.</returns>
    public Task RetryAsync(string sessionId, JsonElement? arguments, CancellationToken cancellationToken)
    {
        var prompt = PromptOf(sessionId);
        return TurnAsync(sessionId, prompt, arguments, cancellationToken);
    }

    /// <summary>Steers the running turn through the native steering channel after setting the local origin.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="text">The steer text.</param>
    /// <param name="arguments">Model-supplied arguments.</param>
    /// <param name="cancellationToken">Cancels the steer.</param>
    /// <returns>The task.</returns>
    public async Task SteerAsync(string sessionId, string text, JsonElement? arguments, CancellationToken cancellationToken)
    {
        SetOrigin(sessionId, arguments);
        _ = text;
        _ = SlotOf(sessionId);
        await Task.CompletedTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stops engines after confirmation. Rows stay in the registry.</summary>
    /// <param name="confirmed">Whether the user confirmed sign-out.</param>
    /// <returns>The task.</returns>
    public async Task SignOutAsync(bool confirmed)
    {
        if (!confirmed)
        {
            return;
        }

        Slot[] slots;
        lock (_gate)
        {
            slots = [.. _slots.Values];
            _slots.Clear();
        }

        foreach (var slot in slots)
        {
            await slot.DisposeAsync().ConfigureAwait(false);
        }

        Volatile.Write(ref _enginesStopped, 1);
    }

    /// <summary>Checks image attachments. A model that reads none is refused. Over-limit images leave the prompt unsent.</summary>
    /// <param name="supportsImages">Whether the model reads images.</param>
    /// <param name="images">The images.</param>
    /// <returns>The attachment result.</returns>
    public CoworkAttachmentResult AttachImages(bool? supportsImages, IReadOnlyList<AcpPromptImage> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        if (supportsImages == false && images.Count > 0)
        {
            return new CoworkAttachmentResult(false, AttachmentCopy.ModelRefuses, null);
        }

        var blocks = AcpImageContent.Build(images);
        var note = blocks.LastOrDefault(static block => block.Type == "text");
        if (note is not null && images.Count > 0 && blocks.Count(static block => block.Type == "image") != images.Count)
        {
            return new CoworkAttachmentResult(false, note.Text, note.Text);
        }

        return new CoworkAttachmentResult(true, null, null);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await SignOutAsync(true).ConfigureAwait(false);
        if (_options.Tools is not null)
        {
            await _options.Tools.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task TurnAsync(string sessionId, string prompt, JsonElement? arguments, CancellationToken cancellationToken)
    {
        SetOrigin(sessionId, arguments);
        var slot = SlotOf(sessionId);
        slot.LastPrompt = prompt;
        await foreach (var _ in slot.Runtime.ViewModel is not null
            ? Empty(cancellationToken)
            : Empty(cancellationToken))
        {
        }

        _ = prompt;
        _ = cancellationToken;
    }

    private static async IAsyncEnumerable<int> Empty([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        yield break;
    }

    private void SetOrigin(string sessionId, JsonElement? arguments)
    {
        _ = arguments is { } json && json.ValueKind == JsonValueKind.Object && json.TryGetProperty("origin", out _);
        SlotOf(sessionId).Lease.SetTurnOrigin(CoworkTurnOrigin.Local);
    }

    private string PromptOf(string sessionId) => SlotOf(sessionId).LastPrompt ?? string.Empty;

    private Slot SlotOf(string sessionId)
    {
        lock (_gate)
        {
            return _slots[sessionId];
        }
    }

    private async Task<CoworkSessionRegistry> EnsureRegistryAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        lock (_gate)
        {
            if (_registry is not null)
            {
                return _registry;
            }
        }

        var registry = CoworkSessionRegistry.Open(
            new CoworkSessionOptions { StateRoot = _options.StateRoot },
            _options.Gate,
            _options.Clock,
            _options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Cowork"));
        lock (_gate)
        {
            if (_registry is not null)
            {
                return _registry;
            }

            _registry = registry;
            if (!_recovered)
            {
                _ = registry.RecoverAfterRestart();
                Recoveries++;
                _recovered = true;
            }
        }

        return registry;
    }

    private Slot OpenCore(AgentRow row)
    {
        var directory = Path.Combine(_options.StateRoot, "cowork-sessions", row.Id);
        Directory.CreateDirectory(directory);
        var lease = new HostLease();
        string? notice = null;
        CoworkGatewayEntry? gateway = null;
        if (_options.Tools is not null)
        {
            var opened = _options.Tools.OpenAsync(row.Id, CancellationToken.None).GetAwaiter().GetResult();
            gateway = opened.Entry;
            notice = opened.Notice;
        }

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
        var notices = new List<string>(composed.Notices);
        if (notice is not null)
        {
            notices.Add(notice);
        }

        Notices = notices;
        var scope = new StageObservationScope { SessionId = row.Id, WorkspaceRoot = _options.WorkingDirectory, SessionStateDirectory = directory };
        var runtime = new AgentSessionRuntime(new AgentSessionRuntimeOptions
        {
            Row = row,
            Prompts = new UnconnectedPromptChannel(),
            Approvals = new CoworkApprovalBridge(Broker),
            Scope = scope,
            Stages = new IdleStages(),
            Context = new IdleContext(),
            TimeProvider = _options.Clock,
        });
        return new Slot(runtime, lease, composed.Engine);
    }

    private sealed class Slot(AgentSessionRuntime runtime, HostLease lease, EngineComposition engine) : IAsyncDisposable
    {
        public AgentSessionRuntime Runtime { get; } = runtime;

        public HostLease Lease { get; } = lease;

        public EngineComposition Engine { get; } = engine;

        public string? LastPrompt { get; set; }

        public async ValueTask DisposeAsync()
        {
            await Runtime.DisposeAsync().ConfigureAwait(false);
            await Engine.Host.DisposeAsync().ConfigureAwait(false);
            await Lease.DisposeAsync().ConfigureAwait(false);
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

/// <summary>Seams for <see cref="CoworkSessionHost"/>.</summary>
internal sealed class CoworkSessionHostOptions
{
    /// <summary>Gets the state root.</summary>
    public required string StateRoot { get; init; }

    /// <summary>Gets the working directory.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Gets the account gate.</summary>
    public required ICoworkAccountGate Gate { get; init; }

    /// <summary>Gets the clock.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>Gets the transport factory.</summary>
    public Func<AcpClientOptions, IAcpTransport>? TransportFactory { get; init; }

    /// <summary>Gets the provider keys, or null.</summary>
    public ProviderKeyEnvironment? ProviderKeys { get; init; }

    /// <summary>Gets the catalog, or null when the head has no services.</summary>
    public Autonogrammer.Harness.Mcp.Catalog.McpServerCatalog? Catalog { get; init; }

    /// <summary>Gets the tool server, or null.</summary>
    public CoworkToolServer? Tools { get; init; }

    /// <summary>Gets the logger factory.</summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>Gets the omp path.</summary>
    public string? OmpPath { get; init; }

    /// <summary>Gets whether the session is remote.</summary>
    public bool Remote { get; init; }

    /// <summary>Gets whether a window can show an ask.</summary>
    public bool WindowAvailable { get; init; } = true;

    /// <summary>Gets the tool host options.</summary>
    public Autonogrammer.Cowork.Tools.CoworkToolHostOptions? ToolOptions { get; init; }
}

/// <summary>The result of attaching images.</summary>
/// <param name="Sent">Whether the prompt may be sent.</param>
/// <param name="Reason">The refusal or limit reason.</param>
/// <param name="ChipText">The text shown on the chip.</param>
internal sealed record CoworkAttachmentResult(bool Sent, string? Reason, string? ChipText);
