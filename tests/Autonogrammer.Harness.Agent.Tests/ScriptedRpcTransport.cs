using System.Threading.Channels;
using Autonogrammer.Harness.Rpc;

namespace Autonogrammer.Harness.Agent.Tests;

/// <summary>
/// An <see cref="IRpcTransport"/> whose peer is a script. Every command line the session writes is handed to the
/// responder, and the lines it returns are delivered to the session in order.
/// </summary>
internal sealed class ScriptedRpcTransport : IRpcTransport
{
    private readonly Channel<string> _stdout = Channel.CreateUnbounded<string>();
    private readonly Func<string, List<string>> _respond;

    /// <summary>Initializes a new instance of the <see cref="ScriptedRpcTransport"/> class.</summary>
    /// <param name="respond">Answers each command line with the lines to deliver.</param>
    /// <param name="connectionFrames">The frames the engine sends before any command, delivered in order.</param>
    public ScriptedRpcTransport(Func<string, List<string>> respond, IEnumerable<string> connectionFrames)
    {
        _respond = respond;
        foreach (string frame in connectionFrames)
        {
            Emit(frame);
        }
    }

    /// <summary>Delivers one stdout line to the session.</summary>
    /// <param name="line">The frame line.</param>
    public void Emit(string line) => _ = _stdout.Writer.TryWrite(line);

    /// <inheritdoc />
    public IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken) =>
        _stdout.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public Task SendLineAsync(string line, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (string reply in _respond(line))
        {
            Emit(reply);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CloseInputAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(0);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _ = _stdout.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
