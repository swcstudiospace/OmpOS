using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.ContractTests;

/// <summary>
/// One side of an in-memory ACP loopback pair (see <see cref="LoopbackAcpTransportPair"/>). Frames are
/// copied on send, so the receiver never aliases the sender's buffer. Closing an endpoint completes both
/// directions: its own receive loop and its peer's, which then observe <see cref="DisconnectedHarnessException"/>.
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Named by the public LoopbackAcpTransportContractTests generic argument; consumed by the contract fixture.")]
public sealed class LoopbackAcpTransport : IAcpTransport
{
    private readonly Channel<byte[]> _inbound = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    private readonly System.Threading.Lock _gate = new();
    private LoopbackAcpTransport? _peer;
    private bool _connected;
    private bool _closed;

    internal LoopbackAcpTransport()
    {
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_closed)
            {
                throw new DisconnectedHarnessException("Loopback endpoint is closed.", "loopback");
            }

            _connected = true;
        }
    }

    /// <inheritdoc />
    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        LoopbackAcpTransport peer;
        lock (_gate)
        {
            EnsureOpen();
            peer = _peer ?? throw new InvalidOperationException("Loopback endpoint has no peer.");
        }

        try
        {
            await peer._inbound.Writer.WriteAsync(frame.ToArray(), cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException ex)
        {
            throw new DisconnectedHarnessException("Loopback peer is closed.", "loopback", ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            lock (_gate)
            {
                EnsureOpen();
            }

            if (!await _inbound.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new DisconnectedHarnessException("Loopback transport closed.", "loopback");
            }

            if (_inbound.Reader.TryRead(out var frame))
            {
                yield return frame;
            }
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        LoopbackAcpTransport? peer;
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            peer = _peer;
        }

        _inbound.Writer.TryComplete();
        peer?._inbound.Writer.TryComplete();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await CloseAsync(CancellationToken.None).ConfigureAwait(false);

    internal void LinkTo(LoopbackAcpTransport peer) => _peer = peer;

    private void EnsureOpen()
    {
        if (!_connected || _closed)
        {
            throw new DisconnectedHarnessException("Loopback endpoint is not connected or is closed.", "loopback");
        }
    }
}
