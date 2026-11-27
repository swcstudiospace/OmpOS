using System.Collections.Frozen;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Rpc;

/// <summary>
/// The <see cref="IOmpSession"/> returned by <see cref="RpcHarnessSessionFactory"/>. Creating it spawns nothing: omp
/// starts on the first <see cref="IOmpSession.CreateAsync"/> or <see cref="IOmpSession.ResumeAsync"/>, through the pinned start path. Every
/// later call is delegated to the started session.
/// </summary>
/// <remarks>
/// A session starts at most once. A failed start leaves it <see cref="HarnessState.Faulted"/>, and a replacement
/// must be created from the factory. Disposing a session that never started is a no-op for the engine.
/// The started session's satellite surfaces (live steering, identity, commands, read-only model control and
/// the empty options surface) are forwarded through this wrapper so hosts that bind satellites by casting
/// the factory-produced session see them; before the start they report their empty/refusing values.
/// </remarks>
internal sealed class RpcHarnessSession
    : IOmpSession, IOmpLiveSteering, IOmpSessionIdentity, IOmpSessionCommands, IOmpModelControl, IOmpSessionOptions
{
    private readonly IRpcSessionStarter _starter;
    private readonly System.Threading.Lock _gate = new();
    private IOmpSession? _started;
    private bool _starting;
    private bool _faulted;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="RpcHarnessSession"/> class. Starts nothing.</summary>
    /// <param name="starter">Opens the engine session on first use.</param>
    public RpcHarnessSession(IRpcSessionStarter starter)
    {
        ArgumentNullException.ThrowIfNull(starter);
        _starter = starter;
    }

    /// <inheritdoc />
    public HarnessState State
    {
        get
        {
            lock (_gate)
            {
                return StateLocked();
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> AdvertisedCapabilities
    {
        get
        {
            lock (_gate)
            {
                return _started?.AdvertisedCapabilities ?? FrozenDictionary<string, string>.Empty;
            }
        }
    }

    /// <inheritdoc />
    public string? NativeSessionId => (Started as IOmpSessionIdentity)?.NativeSessionId;

    /// <inheritdoc />
    public IReadOnlyList<OmpCommand> Commands => (Started as IOmpSessionCommands)?.Commands ?? [];

    /// <inheritdoc />
    public event EventHandler? CommandsChanged;

    /// <inheritdoc />
    public IReadOnlyList<OmpModelChoice> ModelChoices => (Started as IOmpModelControl)?.ModelChoices ?? [];

    /// <inheritdoc />
    public string? CurrentModelId => (Started as IOmpModelControl)?.CurrentModelId;

    /// <inheritdoc />
    public bool? SupportsImages => (Started as IOmpModelControl)?.SupportsImages;

    /// <inheritdoc />
    public event EventHandler? ModelChanged;

    /// <inheritdoc />
    public IReadOnlyList<OmpSessionOption> Options => (Started as IOmpSessionOptions)?.Options ?? [];

    /// <inheritdoc />
    public event EventHandler? OptionsChanged;

    /// <inheritdoc />
    public bool IsSupported => (Started as IOmpLiveSteering)?.IsSupported ?? false;

    private IOmpSession? Started
    {
        get
        {
            lock (_gate)
            {
                return _started;
            }
        }
    }

    /// <summary>Starts omp with a new session and completes the pinned handshake.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <exception cref="ProcessStartHarnessException">omp is missing or outside the pinned release line.</exception>
    /// <exception cref="InvalidHarnessTransitionException">The session was already started.</exception>
    public Task CreateAsync(CancellationToken cancellationToken) =>
        StartAsync(_starter.CreateAsync, cancellationToken);

    /// <summary>Starts omp and switches it to an existing session file.</summary>
    /// <param name="sessionId">The session file path, which omp addresses sessions by.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <exception cref="ArgumentException"><paramref name="sessionId"/> is null, empty, or whitespace.</exception>
    public Task ResumeAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return StartAsync(token => _starter.ResumeAsync(sessionId, token), cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidHarnessTransitionException">The session has not been started.</exception>
    public IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, CancellationToken cancellationToken) =>
        RequireStarted().SubmitPromptAsync(prompt, options, cancellationToken);

    /// <inheritdoc />
    /// <remarks>A session that never started has no prompt in flight, so cancelling it completes immediately.</remarks>
    public Task CancelAsync(CancellationToken cancellationToken)
    {
        IOmpSession? started = Started;
        return started is null ? Task.CompletedTask : started.CancelAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>Before the start this reports <see cref="OmpSteerOutcome.NotRunning"/> and sends nothing, mirroring <see cref="CancelAsync"/>.</remarks>
    public Task<OmpSteerResult> SteerAsync(string text, OmpSteerLane queue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Started switch
        {
            null => Task.FromResult(new OmpSteerResult(OmpSteerOutcome.NotRunning, queue, "No turn is running.")),
            IOmpLiveSteering steering => steering.SteerAsync(text, queue, cancellationToken),
            _ => Task.FromResult(new OmpSteerResult(OmpSteerOutcome.Unsupported, queue, "This session does not offer live steering.")),
        };
    }

    /// <inheritdoc />
    public Task<OmpModelSwitchResult> SetModelAsync(string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Started switch
        {
            null => Task.FromResult(new OmpModelSwitchResult(OmpModelSwitchOutcome.NotOpen, null, "The session is not open.")),
            IOmpModelControl control => control.SetModelAsync(value, cancellationToken),
            _ => Task.FromResult(new OmpModelSwitchResult(OmpModelSwitchOutcome.NoModelOption, null, "This session offers no model control.")),
        };
    }

    /// <inheritdoc />
    public Task<OmpOptionSetResult> SetOptionAsync(string configId, string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configId);
        ArgumentNullException.ThrowIfNull(value);
        return Started switch
        {
            null => Task.FromResult(new OmpOptionSetResult(OmpOptionSetOutcome.NotOpen, null, "The session is not open.")),
            IOmpSessionOptions options => options.SetOptionAsync(configId, value, cancellationToken),
            _ => Task.FromResult(new OmpOptionSetResult(OmpOptionSetOutcome.UnknownOption, null, "This session offers no options.")),
        };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IOmpSession? started;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            started = _started;
            _started = null;
        }

        if (started is not null)
        {
            Detach(started);
            await started.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Runs the single start. A failure marks the session faulted; a disposal during the start disposes the new engine session.</summary>
    /// <param name="start">Opens the engine session.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    private async Task StartAsync(Func<CancellationToken, Task<IOmpSession>> start, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_starting)
            {
                throw new InvalidHarnessTransitionException(StateLocked(), HarnessState.Connecting, "omp rpc session was already started; create a new session");
            }

            _starting = true;
        }

        IOmpSession session;
        try
        {
            session = await start(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (_gate)
            {
                _faulted = true;
            }

            throw;
        }

        bool disposedMeanwhile;
        lock (_gate)
        {
            disposedMeanwhile = _disposed;
            if (!disposedMeanwhile)
            {
                _started = session;
            }
        }

        if (disposedMeanwhile)
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw new ObjectDisposedException(nameof(RpcHarnessSession));
        }

        Attach(session);
    }

    /// <summary>
    /// Forwards the started session's change events under this wrapper. Subscribers that attached before the
    /// start also get one raise per non-empty surface, since frames processed during the handshake preceded
    /// the forwarding.
    /// </summary>
    private void Attach(IOmpSession session)
    {
        if (session is IOmpSessionCommands commands)
        {
            commands.CommandsChanged += OnInnerCommandsChanged;
            if (commands.Commands.Count > 0)
            {
                CommandsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        if (session is IOmpModelControl control)
        {
            control.ModelChanged += OnInnerModelChanged;
            if (control.CurrentModelId is not null)
            {
                ModelChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        if (session is IOmpSessionOptions options)
        {
            options.OptionsChanged += OnInnerOptionsChanged;
        }
    }

    private void Detach(IOmpSession session)
    {
        if (session is IOmpSessionCommands commands)
        {
            commands.CommandsChanged -= OnInnerCommandsChanged;
        }

        if (session is IOmpModelControl control)
        {
            control.ModelChanged -= OnInnerModelChanged;
        }

        if (session is IOmpSessionOptions options)
        {
            options.OptionsChanged -= OnInnerOptionsChanged;
        }
    }

    private void OnInnerCommandsChanged(object? sender, EventArgs args) => CommandsChanged?.Invoke(this, EventArgs.Empty);

    private void OnInnerModelChanged(object? sender, EventArgs args) => ModelChanged?.Invoke(this, EventArgs.Empty);

    private void OnInnerOptionsChanged(object? sender, EventArgs args) => OptionsChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Returns the started session, or throws when a prompt is submitted before the start.</summary>
    /// <returns>The started session.</returns>
    private IOmpSession RequireStarted()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _started ?? throw new InvalidHarnessTransitionException(
                StateLocked(),
                HarnessState.Streaming,
                "omp rpc session has not been started; call CreateAsync before SubmitPromptAsync");
        }
    }

    /// <summary>Derives the observable state. The caller holds <c>_gate</c>.</summary>
    /// <returns>The current state.</returns>
    private HarnessState StateLocked()
    {
        if (_disposed)
        {
            return HarnessState.Disposed;
        }

        if (_started is { } started)
        {
            return started.State;
        }

        if (_faulted)
        {
            return HarnessState.Faulted;
        }

        return _starting ? HarnessState.Connecting : HarnessState.Disconnected;
    }
}
