using System.Text;
using System.Threading.Channels;

namespace OmpOS.Web.Demo;

/// <summary>
/// The body stream of a scripted server-sent-events response. A producer writes frames with <see cref="WriteFrame"/>; the reader gets each frame as
/// soon as it is written, so the browser sees the text arrive at the scripted cadence. Disposing the stream stops the producer.
/// </summary>
internal sealed class DemoSseStream : Stream
{
    private readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _closed = new();
    private byte[] _current = [];
    private int _offset;
    private int _disposed;

    /// <summary>Gets the token that is cancelled when the reader disposes the stream.</summary>
    internal CancellationToken Closed => _closed.Token;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Writes one frame: <c>id</c> (when set), <c>event</c> and <c>data</c> lines and the blank line.</summary>
    /// <param name="id">The event id, or null.</param>
    /// <param name="eventName">The event name.</param>
    /// <param name="data">The data, one line of JSON.</param>
    internal void WriteFrame(string? id, string eventName, string data)
    {
        var frame = new StringBuilder();
        if (id is not null)
        {
            _ = frame.Append("id: ").Append(id).Append('\n');
        }

        _ = frame.Append("event: ").Append(eventName).Append('\n').Append("data: ").Append(data).Append("\n\n");
        _ = _chunks.Writer.TryWrite(Encoding.UTF8.GetBytes(frame.ToString()));
    }

    /// <summary>Ends the stream: the reader sees the end of the body after the frames already written.</summary>
    internal void Complete() => _ = _chunks.Writer.TryComplete();

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (_offset >= _current.Length)
        {
            if (!await _chunks.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return 0;
            }

            if (_chunks.Reader.TryRead(out var next))
            {
                _current = next;
                _offset = 0;
            }
        }

        var count = Math.Min(buffer.Length, _current.Length - _offset);
        _current.AsMemory(_offset, count).CopyTo(buffer);
        _offset += count;
        return count;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("The Demo stream is read asynchronously.");

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _closed.Cancel();
            _ = _chunks.Writer.TryComplete();
            _closed.Dispose();
        }

        base.Dispose(disposing);
    }
}
