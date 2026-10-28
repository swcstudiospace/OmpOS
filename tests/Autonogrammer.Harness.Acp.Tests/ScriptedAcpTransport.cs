using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// An in-memory <see cref="IAcpTransport"/> whose peer is a script. Every sent frame is recorded, then handed to the
/// responder; the frames it returns are delivered to the receiver in order. Tests can also inject agent-initiated frames.
/// </summary>
internal sealed class ScriptedAcpTransport : IAcpTransport
{
    private readonly Channel<byte[]> _inbound = Channel.CreateUnbounded<byte[]>();
    private readonly Func<byte[], IEnumerable<byte[]>> _responder;
    private readonly List<byte[]> _sent = [];
    private readonly System.Threading.Lock _gate = new();
    private bool _closed;

    public ScriptedAcpTransport(Func<byte[], IEnumerable<byte[]>>? responder = null)
    {
        _responder = responder ?? (static _ => []);
    }

    /// <summary>Gets a snapshot of every frame the session has sent, in order.</summary>
    public IReadOnlyList<byte[]> Sent
    {
        get
        {
            lock (_gate)
            {
                return _sent.ToArray();
            }
        }
    }

    /// <summary>Delivers a frame to the session as if the agent sent it.</summary>
    public void Deliver(byte[] frame) => _inbound.Writer.TryWrite(frame);

    /// <summary>Simulates the agent dropping the connection.</summary>
    public void Disconnect() => _inbound.Writer.TryComplete();

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
        var copy = frame.ToArray();
        lock (_gate)
        {
            if (_closed)
            {
                throw new DisconnectedHarnessException("The scripted transport is closed.", "scripted");
            }

            _sent.Add(copy);
        }

        foreach (var reply in _responder(copy))
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
