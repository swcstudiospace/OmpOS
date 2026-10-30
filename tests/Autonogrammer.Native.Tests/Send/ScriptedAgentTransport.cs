using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Native.Tests.Send;

/// <summary>
/// An in-memory <see cref="IAcpTransport"/> whose peer is a script. Every frame the client sends is handed to the responder, and the
/// frames it returns are delivered back to the client in order. No process is spawned.
/// </summary>
internal sealed class ScriptedAgentTransport(Func<byte[], IEnumerable<byte[]>> responder) : IAcpTransport
{
    private readonly Channel<byte[]> _inbound = Channel.CreateUnbounded<byte[]>();
    private readonly System.Threading.Lock _gate = new();
    private bool _closed;

    /// <summary>Delivers frames to the client as the peer sends them on its own, outside any client request.</summary>
    /// <param name="frames">The frames to deliver, in order.</param>
    public void Push(IEnumerable<byte[]> frames)
    {
        foreach (var frame in frames)
        {
            _inbound.Writer.TryWrite(frame);
        }
    }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_closed)
            {
                throw new DisconnectedHarnessException("The scripted transport is closed.", "scripted");
            }
        }

        foreach (var reply in responder(frame.ToArray()))
        {
            _inbound.Writer.TryWrite(reply);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            if (!await _inbound.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new DisconnectedHarnessException("The scripted transport closed.", "scripted");
            }

            while (_inbound.Reader.TryRead(out var frame))
            {
                yield return frame;
            }
        }
    }

    /// <inheritdoc />
    public Task CloseAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _closed = true;
        }

        _inbound.Writer.TryComplete();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await CloseAsync(CancellationToken.None).ConfigureAwait(false);
}
