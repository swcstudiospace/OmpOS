using System.Text;
using System.Threading.Channels;
using Autonogrammer.Harness.Terminal;
using Porta.Pty;

namespace Autonogrammer.Native.Views.Terminal;

/// <summary>
/// Joins the terminal control to an <see cref="ITerminalSession"/> that the control does not own. The control draws what it
/// reads from <see cref="IPtyConnection.ReaderStream"/>, turns keys, paste, mouse and its own answers (such as the device
/// attributes reply omp asks for) into text written to <see cref="IPtyConnection.WriterStream"/>, and reports its grid size
/// through <see cref="IPtyConnection.Resize"/>. The bridge feeds the first from the terminal output and sends the other
/// two to the session, so the session stays the only owner of the pseudo-terminal and of the omp process.
/// </summary>
/// <remarks>
/// The bridge outlives the sessions: one bridge serves every start of omp in the view, and <see cref="AttachSession"/>
/// points it at the current session. Input typed while no session is attached is dropped, because no process reads it.
/// The bridge never reports an exit itself: the view model watches the session and shows the exit.
/// </remarks>
internal sealed class OmpTerminalPtyBridge : IPtyConnection
{
    private readonly Channel<byte[]> _output = Channel.CreateUnbounded<byte[]>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly OutputStream _reader;
    private readonly InputStream _writer;
    private readonly Action<int, int> _onResize;
    private readonly Lock _gate = new();
    private ITerminalSession? _session;
    private (int Columns, int Rows)? _size;

    /// <summary>Creates a bridge.</summary>
    /// <param name="onResize">Called with the new size each time the control reports a terminal size. It may run on any thread.</param>
    /// <exception cref="ArgumentNullException"><paramref name="onResize"/> is null.</exception>
    public OmpTerminalPtyBridge(Action<int, int> onResize)
    {
        ArgumentNullException.ThrowIfNull(onResize);
        _onResize = onResize;
        _reader = new OutputStream(_output.Reader);
        _writer = new InputStream(this);
        _ = Task.Run(PumpInputAsync);
    }

    /// <inheritdoc/>
    public event EventHandler<PtyExitedEventArgs>? ProcessExited
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public Stream ReaderStream => _reader;

    /// <summary>Gets a value indicating whether a read can be cancelled. It can: the terminal control stops reading that way.</summary>
    public bool SupportsCancellableRead => true;

    /// <inheritdoc/>
    public Stream WriterStream => _writer;

    /// <summary>Gets zero: omp's process id is not the bridge's to report.</summary>
    public int Pid => 0;

    /// <summary>Gets zero. The bridge never reports an exit; the view model shows the real exit code.</summary>
    public int ExitCode => 0;

    /// <summary>
    /// Queues terminal output for the control to draw. Safe to call from any thread, and the order of calls is the order drawn.
    /// </summary>
    /// <param name="text">The output text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public void Write(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > 0)
        {
            _output.Writer.TryWrite(Encoding.UTF8.GetBytes(text));
        }
    }

    /// <summary>
    /// Points the bridge at the session the user types into, or at none. If the control already reported a size, the session is
    /// given it, so a session started at a stale size catches up.
    /// </summary>
    /// <param name="session">The session, or null when no process is running.</param>
    public void AttachSession(ITerminalSession? session)
    {
        (int Columns, int Rows)? size;
        lock (_gate)
        {
            _session = session;
            size = _size;
        }

        if (session is not null && size is { } known)
        {
            ResizeSession(session, known.Columns, known.Rows);
        }
    }

    /// <summary>Remembers the size the control reports and applies it to the attached session.</summary>
    /// <param name="cols">The number of columns.</param>
    /// <param name="rows">The number of rows.</param>
    public void Resize(int cols, int rows)
    {
        if (cols < 1 || rows < 1)
        {
            return;
        }

        ITerminalSession? session;
        lock (_gate)
        {
            _size = (cols, rows);
            session = _session;
        }

        _onResize(cols, rows);
        if (session is not null)
        {
            ResizeSession(session, cols, rows);
        }
    }

    /// <summary>Returns false: the bridge never sees an exit, so waiting for one only runs out the time.</summary>
    /// <param name="milliseconds">Ignored.</param>
    /// <returns>Always false.</returns>
    public bool WaitForExit(int milliseconds) => false;

    /// <summary>
    /// Does nothing. The view model owns the session and stops it through <see cref="OmpTerminalViewModel.StopAsync"/>; the
    /// control's own Kill is never used on a connection it does not own.
    /// </summary>
    public void Kill()
    {
    }

    /// <summary>Ends the output stream and releases the session reference. The session itself is not disposed.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _session = null;
        }

        _output.Writer.TryComplete();
        _input.Writer.TryComplete();
        _reader.Dispose();
        _writer.Dispose();
    }

    /// <summary>Sends control input to the attached session. Input with no session, or for a session that has ended, is dropped.</summary>
    private async ValueTask SendInputAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        var session = CurrentSession();
        if (session is null || buffer.IsEmpty)
        {
            return;
        }

        try
        {
            await session.WriteInputAsync(Encoding.UTF8.GetString(buffer.Span), cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The session ended while the user was typing: no process reads this input any more.
        }
        catch (IOException)
        {
            // omp exited and closed its input: the key has nowhere to go.
        }
    }

    /// <summary>Sends the input the control wrote synchronously, one write at a time and in write order, so the control's thread never waits for omp.</summary>
    private async Task PumpInputAsync()
    {
        await foreach (var bytes in _input.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            await SendInputAsync(bytes, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private ITerminalSession? CurrentSession()
    {
        lock (_gate)
        {
            return _session;
        }
    }

    private static void ResizeSession(ITerminalSession session, int columns, int rows)
    {
        try
        {
            session.Resize(columns, rows);
        }
        catch (ObjectDisposedException)
        {
            // The session was disposed between the lookup and the resize. A process that is gone has no size to change.
        }
    }

    /// <summary>The control's read side: the queued terminal output as bytes.</summary>
    private sealed class OutputStream(ChannelReader<byte[]> reader) : Stream
    {
        private byte[] _pending = [];
        private int _offset;

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Flush()
        {
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException("The terminal output is read asynchronously: a blocking read would hold a thread until omp prints.");

        /// <inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty)
            {
                return 0;
            }

            while (_offset >= _pending.Length)
            {
                if (!await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return 0;
                }

                if (reader.TryRead(out var chunk))
                {
                    _pending = chunk;
                    _offset = 0;
                }
            }

            var count = Math.Min(buffer.Length, _pending.Length - _offset);
            _pending.AsMemory(_offset, count).CopyTo(buffer);
            _offset += count;
            return count;
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>The control's write side: what the user types or pastes, as text for the session.</summary>
    private sealed class InputStream(OmpTerminalPtyBridge bridge) : Stream
    {
        /// <inheritdoc/>
        public override bool CanRead => false;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Flush()
        {
        }

        /// <inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            if (count > 0)
            {
                bridge._input.Writer.TryWrite(buffer.AsSpan(offset, count).ToArray());
            }
        }

        /// <inheritdoc/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        /// <inheritdoc/>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            bridge.SendInputAsync(buffer, cancellationToken);

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
