using System.Buffers;

namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>
/// Reads one request from one connection: first the head, up to a cap, then, when the caller decides to, a body of exactly the
/// announced length. It owns its buffers, which come from the array pool, and returns them when disposed. Bytes of the body
/// that arrived together with the head are used before the stream is read again, and bytes after the body are ignored.
/// </summary>
internal sealed class HookBridgeHttpReader : IDisposable
{
    /// <summary>The value <see cref="ReadHeadAsync"/> returns when the peer closed the connection before a whole head arrived.</summary>
    internal const int PeerClosed = HookBridgeHttpResponse.NoResponse;

    private static ReadOnlySpan<byte> Terminator => "\r\n\r\n"u8;

    private readonly Stream _stream;
    private readonly int _maxHeaderBytes;
    private readonly byte[] _buffer;
    private byte[]? _body;
    private int _bodyLength;
    private int _filled;
    private int _consumed;
    private bool _disposed;

    /// <summary>Creates a reader.</summary>
    /// <param name="stream">The connection. The reader owns it and closes it when disposed.</param>
    /// <param name="maxHeaderBytes">The most bytes the head may have, including its blank line.</param>
    internal HookBridgeHttpReader(Stream stream, int maxHeaderBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxHeaderBytes, 1);
        _stream = stream;
        _maxHeaderBytes = maxHeaderBytes;
        _buffer = ArrayPool<byte>.Shared.Rent(maxHeaderBytes);
    }

    /// <summary>Gets the parsed head after <see cref="ReadHeadAsync"/> returned <see cref="HookBridgeHttpResponse.Ok"/>, otherwise null.</summary>
    internal HookBridgeHttpHead? Head { get; private set; }

    /// <summary>Gets the body after <see cref="ReadBodyAsync"/> returned true and before <see cref="ReleaseBody"/> or disposal.</summary>
    internal ReadOnlyMemory<byte> Body => _body is null ? ReadOnlyMemory<byte>.Empty : _body.AsMemory(0, _bodyLength);

    /// <summary>Reads and parses the request head.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// <see cref="HookBridgeHttpResponse.Ok"/> when <see cref="Head"/> is set; the status to answer with when the head is invalid
    /// (<see cref="HookBridgeHttpResponse.BadRequest"/> or <see cref="HookBridgeHttpResponse.HeadersTooLarge"/>); or
    /// <see cref="PeerClosed"/> when the peer closed the connection first.
    /// </returns>
    internal async ValueTask<int> ReadHeadAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var searchFrom = 0;
        while (true)
        {
            var read = await _stream.ReadAsync(_buffer.AsMemory(_filled, _maxHeaderBytes - _filled), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return PeerClosed;
            }

            _filled += read;
            var found = _buffer.AsSpan(searchFrom, _filled - searchFrom).IndexOf(Terminator);
            if (found >= 0)
            {
                _consumed = searchFrom + found + Terminator.Length;
                var status = HookBridgeHttpParser.Parse(_buffer.AsSpan(0, _consumed), out var head);
                Head = head;
                return status;
            }

            if (_filled == _maxHeaderBytes)
            {
                return HookBridgeHttpResponse.HeadersTooLarge;
            }

            searchFrom = Math.Max(0, _filled - (Terminator.Length - 1));
        }
    }

    /// <summary>Reads the body of the announced length. Call it only after the head was accepted.</summary>
    /// <param name="length">The body length, from the <c>Content-Length</c> header. It is not negative.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the whole body is in <see cref="Body"/>; false when the peer closed the connection first.</returns>
    internal async ValueTask<bool> ReadBodyAsync(int length, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ReleaseBody();
        if (length == 0)
        {
            return true;
        }

        var body = ArrayPool<byte>.Shared.Rent(length);
        _body = body;
        _bodyLength = length;
        var buffered = Math.Min(_filled - _consumed, length);
        _buffer.AsSpan(_consumed, buffered).CopyTo(body);
        _consumed += buffered;
        var total = buffered;
        while (total < length)
        {
            var read = await _stream.ReadAsync(body.AsMemory(total, length - total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            total += read;
        }

        return true;
    }

    /// <summary>Returns the body buffer to the pool, clearing it first, because the body may hold commands or file contents.</summary>
    internal void ReleaseBody()
    {
        var body = _body;
        _body = null;
        _bodyLength = 0;
        if (body is not null)
        {
            ArrayPool<byte>.Shared.Return(body, clearArray: true);
        }
    }

    /// <summary>
    /// Reads and discards what the peer still sends, up to a limit, so that closing the connection does not reset it while the
    /// peer is still reading the answer.
    /// </summary>
    /// <param name="maxBytes">The most bytes to discard.</param>
    /// <param name="cancellationToken">Ends the wait for a peer that keeps the connection open.</param>
    /// <returns>A task that completes when the peer closed the connection or the limit was reached.</returns>
    internal async ValueTask DrainAsync(int maxBytes, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var total = 0;
        while (total < maxBytes)
        {
            var read = await _stream.ReadAsync(_buffer.AsMemory(0, _maxHeaderBytes), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            total += read;
        }
    }

    /// <summary>Returns every buffer to the pool, clearing it first, and closes the connection stream. Calling it again does nothing.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleaseBody();
        ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
        _stream.Dispose();
    }
}
