using System.Globalization;
using System.Runtime.CompilerServices;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.EngineHost;

/// <summary>
/// Keeps a session on a remote machine alive across dropped connections. It wraps an <see cref="InProcessEngineHost"/> whose
/// transport is an ssh tunnel and adds the reconnect behaviour: when the connection drops while a turn runs, the turn is reported
/// interrupted (an <see cref="ErrorEvent"/> that is retryable, never a <see cref="CompletedEvent"/>), the host waits according to
/// the <see cref="ReconnectPolicy"/> and loads the stored engine session again with <c>session/load</c>. After the last failed
/// attempt the state is <see cref="RemoteConnectionState.Unreachable"/> and the next prompt starts another round.
/// </summary>
public sealed class ReconnectingEngineHost : IEngineHost, IAsyncDisposable
{
    private readonly InProcessEngineHost _inner;
    private readonly ReconnectPolicy _policy;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _reconnectGate = new(1, 1);
    private readonly System.Threading.Lock _gate = new();
    private RemoteConnectionState _state = RemoteConnectionState.Disconnected;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="ReconnectingEngineHost"/> class.</summary>
    /// <param name="inner">The host whose connection is kept alive. It must have been created with <c>resumeAfterFault</c> so a reconnect loads the stored session instead of starting an empty one.</param>
    /// <param name="policy">The retry schedule, or <see langword="null"/> for <see cref="ReconnectPolicy.Default"/>.</param>
    /// <param name="clock">The clock the waits use, or <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentException">The inner host does not resume after a fault, or the policy is invalid.</exception>
    public ReconnectingEngineHost(InProcessEngineHost inner, ReconnectPolicy? policy = null, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        if (!inner.ResumesAfterFault)
        {
            throw new ArgumentException("The inner host must be created with resumeAfterFault, or a reconnect would start an empty conversation.", nameof(inner));
        }

        _policy = policy ?? ReconnectPolicy.Default;
        _policy.Validate();
        _inner = inner;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Occurs when the connection state changes. A handler that throws does not affect the host.</summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>Occurs when the connection dropped while a turn was running, before the first reconnect attempt. A handler that throws does not affect the host.</summary>
    public event EventHandler<TurnInterruptedEventArgs>? TurnInterrupted;

    /// <inheritdoc />
    public EngineHostDescriptor Descriptor => _inner.Descriptor;

    /// <summary>Gets the wrapped host, for its model, option, command and steering controls and its native session identifier.</summary>
    public InProcessEngineHost Inner => _inner;

    /// <summary>Gets the connection state.</summary>
    public RemoteConnectionState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>Opens the engine session now. A failure of this first connection is not retried: it is a configuration problem the user must see.</summary>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>A task that completes when the session is open.</returns>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        SetState(RemoteConnectionState.Connecting, 0, null);
        try
        {
            await _inner.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            SetState(RemoteConnectionState.Disconnected, 0, null);
            throw;
        }

        SetState(RemoteConnectionState.Connected, 0, null);
    }

    /// <summary>
    /// Reopens the connection and loads the stored engine session, retrying according to the policy. Does nothing and returns
    /// <see langword="true"/> when the connection is already up.
    /// </summary>
    /// <param name="cancellationToken">Cancels the waits and the attempt in progress.</param>
    /// <returns><see langword="true"/> when the session is loaded again; <see langword="false"/> after the last attempt failed (the state is then <see cref="RemoteConnectionState.Unreachable"/>).</returns>
    public async Task<bool> ReconnectAsync(CancellationToken cancellationToken)
    {
        await _reconnectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State == RemoteConnectionState.Connected)
            {
                return true;
            }

            string? lastFailure = null;
            for (var attempt = 1; attempt <= _policy.MaxAttempts; attempt++)
            {
                SetState(RemoteConnectionState.Reconnecting, attempt, lastFailure);
                await Task.Delay(_policy.DelayBefore(attempt), _clock, cancellationToken).ConfigureAwait(false);
                try
                {
                    await _inner.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    SetState(RemoteConnectionState.Connected, attempt, null);
                    return true;
                }
                catch (Exception ex) when (ex is DisconnectedHarnessException or HandshakeTimeoutHarnessException)
                {
                    lastFailure = ex.Message;
                }
            }

            SetState(RemoteConnectionState.Unreachable, _policy.MaxAttempts, lastFailure);
            return false;
        }
        finally
        {
            _reconnectGate.Release();
        }
    }

    // Opens (or reloads) the session before a turn starts, so a failure here is a connection problem and not an interrupted turn.
    private async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _inner.ConnectAsync(cancellationToken).ConfigureAwait(false);
            if (State != RemoteConnectionState.Connected)
            {
                SetState(RemoteConnectionState.Connected, 0, null);
            }

            return true;
        }
        catch (Exception ex) when (ex is DisconnectedHarnessException or HandshakeTimeoutHarnessException)
        {
            SetState(RemoteConnectionState.Reconnecting, 0, ex.Message);
            return await ReconnectAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
        string prompt,
        PromptOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(options);

        if (!await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new ErrorEvent(UnreachableMessage(), Retryable: true);
            yield break;
        }

        Exception? dropped = null;
        var stream = _inner.SubmitPromptAsync(prompt, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                HarnessEvent? next = null;
                try
                {
                    if (!await stream.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }

                    next = stream.Current;
                }
                catch (Exception ex) when (ex is DisconnectedHarnessException or HandshakeTimeoutHarnessException)
                {
                    dropped = ex;
                }

                if (dropped is not null)
                {
                    break;
                }

                yield return next!;
            }
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }

        if (dropped is null)
        {
            yield break;
        }

        SetState(RemoteConnectionState.Reconnecting, 0, dropped.Message);
        Raise(TurnInterrupted, new TurnInterruptedEventArgs(_inner.NativeSessionId));
        yield return new DiagnosticEvent("The connection to the remote host was lost while the turn was running. Reconnecting.");
        var reconnected = await ReconnectAsync(cancellationToken).ConfigureAwait(false);
        yield return reconnected
            ? new ErrorEvent("The connection dropped during this turn, so the turn did not finish. The remote session was loaded again; send the prompt again.", Retryable: true)
            : new ErrorEvent(UnreachableMessage(), Retryable: true);
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken) => _inner.CancelAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        await _inner.DisposeAsync().ConfigureAwait(false);
        SetState(RemoteConnectionState.Closed, 0, null);
        _reconnectGate.Dispose();
    }

    private string UnreachableMessage() =>
        string.Create(CultureInfo.InvariantCulture, $"The remote host could not be reached after {_policy.MaxAttempts} attempts. Send the prompt again to retry.");

    private void SetState(RemoteConnectionState state, int attempt, string? lastFailure)
    {
        lock (_gate)
        {
            if (_state == RemoteConnectionState.Closed)
            {
                return;
            }

            _state = state;
        }

        Raise(ConnectionStateChanged, new ConnectionStateChangedEventArgs(state, attempt, lastFailure));
    }

    private void Raise<TArgs>(EventHandler<TArgs>? handlers, TArgs args)
        where TArgs : EventArgs
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<TArgs>>())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A subscriber must never fail the turn or hide the change from the other subscribers.
            }
        }
    }
}
