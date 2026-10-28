using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Agent.Tests;

/// <summary>
/// An <see cref="IAcpTransport"/> whose peer is a script. Every frame the session sends is recorded and handed to the
/// responder, and the frames it returns are received by the session in order.
/// </summary>
internal sealed class ScriptedAcpTransport : IAcpTransport
{
    private readonly Channel<byte[]> _inbound = Channel.CreateUnbounded<byte[]>();
    private readonly Func<byte[], List<byte[]>> _respond;
    private readonly List<byte[]> _sent = [];
    private readonly System.Threading.Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="ScriptedAcpTransport"/> class.</summary>
    /// <param name="respond">Answers each frame the session sends with the frames to deliver.</param>
    public ScriptedAcpTransport(Func<byte[], List<byte[]>> respond)
    {
        _respond = respond;
    }

    /// <summary>Gets the method names of the request frames the session sent, in order. Responses carry no method.</summary>
    public List<string> SentMethods()
    {
        lock (_gate)
        {
            var methods = new List<string>();
            foreach (byte[] frame in _sent)
            {
                string? method = JsonNode.Parse(frame)!["method"]?.GetValue<string>();
                if (method is not null)
                {
                    methods.Add(method);
                }
            }

            return methods;
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
        byte[] copy = frame.ToArray();
        lock (_gate)
        {
            _sent.Add(copy);
        }

        foreach (byte[] reply in _respond(copy))
        {
            _ = _inbound.Writer.TryWrite(reply);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _inbound.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (_inbound.Reader.TryRead(out byte[]? frame))
            {
                yield return frame;
            }
        }

        throw new DisconnectedHarnessException("The scripted ACP transport closed.", "scripted");
    }

    /// <inheritdoc />
    public Task CloseAsync(CancellationToken cancellationToken)
    {
        _ = _inbound.Writer.TryComplete();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _ = _inbound.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
