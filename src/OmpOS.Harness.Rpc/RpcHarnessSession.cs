using System.Collections.Frozen;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Rpc;

/// <summary>
/// The <see cref="IOmpSession"/> returned by <see cref="RpcHarnessSessionFactory"/>. Creating it spawns nothing: omp
/// starts on the first <see cref="IOmpSession.CreateAsync"/> or <see cref="IOmpSession.ResumeAsync"/>, through the pinned start path. Every
/// later call is delegated to the started session.
/// </summary>
/// <remarks>
/// A session starts at most once. A failed start leaves it <see cref="HarnessState.Faulted"/>, and a replacement
/// must be created from the factory. Disposing a session that never started is a no-op for the engine.
/// </remarks>
internal sealed class RpcHarnessSession : IOmpSession
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
        IOmpSession? started;
        lock (_gate)
        {
            started = _started;
        }

        return started is null ? Task.CompletedTask : started.CancelAsync(cancellationToken);
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
    }

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
