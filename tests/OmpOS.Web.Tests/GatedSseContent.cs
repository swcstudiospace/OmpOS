using System.Net;
using System.Text;

namespace OmpOS.Web.Tests;

/// <summary>
/// An <see cref="HttpContent"/> that streams the first frame immediately, then blocks all further
/// reads until <see cref="Release"/> is called. Proves the client yields each event as its frame
/// is parsed instead of buffering the whole connection.
/// </summary>
internal sealed class GatedSseContent : HttpContent
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly byte[] _first;
    private readonly byte[] _rest;

    /// <summary>Initializes content that serves <paramref name="first"/> now and <paramref name="rest"/> after <see cref="Release"/>.</summary>
    /// <param name="first">The frame bytes served immediately.</param>
    /// <param name="rest">The frame bytes served after release.</param>
    internal GatedSseContent(string first, string rest)
    {
        _first = Encoding.UTF8.GetBytes(first);
        _rest = Encoding.UTF8.GetBytes(rest);
        Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
    }

    /// <summary>Unblocks the remainder of the body. Idempotent.</summary>
    internal void Release() => _gate.TrySetResult();

    /// <inheritdoc />
    protected override Task<Stream> CreateContentReadStreamAsync() =>
        Task.FromResult<Stream>(new GatedReadStream(_first, _rest, _gate.Task));

    /// <inheritdoc />
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        throw new NotSupportedException("GatedSseContent streams through CreateContentReadStreamAsync.");

    /// <inheritdoc />
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
        throw new NotSupportedException("GatedSseContent streams through CreateContentReadStreamAsync.");

    /// <inheritdoc />
    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    private sealed class GatedReadStream : Stream
    {
        private readonly byte[] _first;
        private readonly byte[] _rest;
        private readonly Task _gate;
        private int _stage;
        private int _offset;

        internal GatedReadStream(byte[] first, byte[] rest, Task gate)
        {
            _first = first;
            _rest = rest;
            _gate = gate;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            byte[] current;
            if (_stage == 0)
            {
                current = _first;
            }
            else if (_stage == 1)
            {
#pragma warning disable VSTHRD003 // Test helper: xUnit provides no SynchronizationContext, and the gate completes via RunContinuationsAsynchronously.
                await _gate.ConfigureAwait(false);
#pragma warning restore VSTHRD003
                cancellationToken.ThrowIfCancellationRequested();
                current = _rest;
            }
            else
            {
                return 0;
            }

            var count = Math.Min(buffer.Length, current.Length - _offset);
            current.AsMemory(_offset, count).CopyTo(buffer);
            _offset += count;
            if (_offset >= current.Length)
            {
                _stage++;
                _offset = 0;
            }

            return count;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_stage >= 2)
            {
                return 0;
            }

            if (_stage == 1 && !_gate.IsCompleted)
            {
                throw new IOException("GatedReadStream blocks until Release; sync reads cannot wait.");
            }

            var current = _stage == 0 ? _first : _rest;
            var take = Math.Min(count, current.Length - _offset);
            Array.Copy(current, _offset, buffer, offset, take);
            _offset += take;
            if (_offset >= current.Length)
            {
                _stage++;
                _offset = 0;
            }

            return take;
        }
        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
