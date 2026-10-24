using System.Text;
using System.Threading.Channels;

namespace OmpOS.Harness.Terminal;

/// <summary>
/// The interactive session over a <see cref="PtyCore"/>. It starts the output pump when it is created, serializes input
/// writes so concurrent callers cannot interleave bytes, and on disposal kills the process if it is still running and waits
/// for its output to end before it releases the terminal.
/// </summary>
/// <remarks>
/// Writes take a single token from a one-slot channel, so one write uses the terminal at a time. Disposal takes the same
/// token after the kill, so no write reaches the connection while it is being disposed. A write still waiting when
/// disposal begins fails with <see cref="ObjectDisposedException"/>. A write in progress when disposal begins is cancelled
/// through its stream, which releases it only if the stream honours cancellation. Resize is synchronous and must not wait
/// behind a write, so it is serialized with disposal by its own lock instead.
/// </remarks>
internal sealed class PtyTerminalSession : ITerminalSession
{
    /// <summary>
    /// The longest disposal waits for the output to end after the process is killed. A background process that inherited
    /// the terminal can keep the output open. After this bound the terminal is released anyway, and output still pending
    /// may be lost.
    /// </summary>
    private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(5);

    private readonly PtyCore _core;
    private readonly Task _output;
    private readonly Channel<bool> _writeToken = Channel.CreateBounded<bool>(1);
    private readonly CancellationTokenSource _disposing = new();
    private readonly Lock _resizeLock = new();
    private int _disposed;

    /// <summary>
    /// Takes ownership of a spawned terminal and starts delivering its output to the sink.
    /// </summary>
    /// <param name="core">The spawned terminal. The session disposes it.</param>
    /// <param name="output">Receives the output as it arrives.</param>
    public PtyTerminalSession(PtyCore core, IProcessOutputSink output)
    {
        _core = core;
        _writeToken.Writer.TryWrite(true);
        _output = core.PumpOutputAsync(text => output.Write(text.ToString()));

        // A faulted pump is observed here on purpose, so it is never an unobserved task. The fault still reaches
        // WaitForExitAsync, which awaits the pump, and disposal does not rethrow it.
        _ = _output.ContinueWith(
            static task => { _ = task.Exception; },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <inheritdoc />
    public async Task WriteInputAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        var bytes = Encoding.UTF8.GetBytes(text);
        await TakeWriteTokenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);

            // Disposal cancels _disposing, which releases a write whose stream honours its token.
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposing.Token);
            try
            {
                await _core.WriteAsync(bytes, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (_disposed != 0 && !cancellationToken.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(PtyTerminalSession), ex);
            }
        }
        finally
        {
            // After disposal the channel is completed, so returning the token does nothing and never throws.
            _writeToken.Writer.TryWrite(true);
        }
    }

    /// <inheritdoc />
    public void Resize(int columns, int rows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);

        lock (_resizeLock)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (!_core.Exited.IsCompleted)
            {
                _core.Resize(columns, rows);
            }
        }
    }

    /// <inheritdoc />
    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        var exitCode = await _core.Exited.WaitAsync(cancellationToken).ConfigureAwait(false);
        await _output.WaitAsync(cancellationToken).ConfigureAwait(false);
        return exitCode;
    }

    /// <summary>
    /// Kills the process if it is still running, cancels any write in progress, waits for the output to end for at most
    /// <see cref="OutputDrainTimeout"/>, and then releases the terminal. The release happens even if the output pump faulted.
    /// Writes still waiting fail with <see cref="ObjectDisposedException"/>.
    /// </summary>
    /// <remarks>
    /// Bounded: the output wait, to <see cref="OutputDrainTimeout"/>. A pump fault is observed and never rethrown from disposal.
    /// Not bounded: a write that has already started and that the terminal does not complete. Disposal takes the write token
    /// only after that write returns, so it waits for that write. The cancellation above releases it only if the stream
    /// honours cancellation, which is not verified against Porta.Pty's stream.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            if (!_core.Exited.IsCompleted)
            {
                _core.Kill();
            }

            await _disposing.CancelAsync().ConfigureAwait(false);

            // Bounded and never throws: the drain continuation completes when the output ends (faulted or not), so WhenAny
            // completes then or at the drain bound. It never rethrows a fault, so the release below always runs.
            var drained = _output.ContinueWith(
                static _ => { },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            await Task.WhenAny(drained, Task.Delay(OutputDrainTimeout)).ConfigureAwait(false);

            // Taking the token waits for a write already in progress. Writes queued behind this call fail once the channel completes.
            _ = await _writeToken.Reader.ReadAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                lock (_resizeLock)
                {
                    _core.Dispose();
                }
            }
            finally
            {
                // The token is never returned. Every write still waiting, and every later one, fails with ObjectDisposedException.
                _writeToken.Writer.TryComplete();
                _disposing.Dispose();
            }
        }
    }

    private async Task TakeWriteTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ = await _writeToken.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException ex)
        {
            throw new ObjectDisposedException(nameof(PtyTerminalSession), ex);
        }
    }
}
