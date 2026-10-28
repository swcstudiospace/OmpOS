using System.Runtime.CompilerServices;
using Autonogrammer.Presentation.Terminal;

namespace Autonogrammer.Presentation.Tests.Terminal;

/// <summary>
/// A scripted remote terminal. Each attach plays the next script: its chunks, then either a broken connection or a clean end.
/// Every attach records the sequence the caller said it had already received.
/// </summary>
internal sealed class FakeRemoteTerminalTransport : IRemoteTerminalTransport
{
    private readonly Queue<Script> _scripts = new();

    /// <summary>Gets the <c>afterSequence</c> argument of every attach, in call order.</summary>
    public List<long> AttachRequests { get; } = [];

    /// <summary>Queues the chunks and ending of the next attach.</summary>
    /// <param name="breakConnection">True to throw <see cref="IOException"/> after the last chunk; false to end the stream cleanly.</param>
    /// <param name="chunks">The chunks to deliver, in the order given (a script may repeat or skip sequences).</param>
    public void Enqueue(bool breakConnection, params RemoteTerminalChunk[] chunks) =>
        _scripts.Enqueue(new Script(chunks, breakConnection));

    /// <inheritdoc/>
    public IAsyncEnumerable<RemoteTerminalChunk> AttachAsync(string terminalId, long afterSequence, CancellationToken cancellationToken)
    {
        AttachRequests.Add(afterSequence);
        return PlayAsync(_scripts.Dequeue(), cancellationToken);
    }

    private static async IAsyncEnumerable<RemoteTerminalChunk> PlayAsync(Script script, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (RemoteTerminalChunk chunk in script.Chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask.ConfigureAwait(true);
            yield return chunk;
        }

        if (script.BreakConnection)
        {
            throw new IOException("The connection was lost.");
        }
    }

    private sealed record Script(RemoteTerminalChunk[] Chunks, bool BreakConnection);
}
