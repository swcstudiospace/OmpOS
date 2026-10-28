using System.Runtime.CompilerServices;
using OmpOS.Presentation.Terminal;

namespace OmpOS.UI.Tests;

/// <summary>A remote terminal transport the test scripts: each attach asks the script what the stream does, and every attach is recorded.</summary>
internal sealed class ScriptedTerminalTransport : IRemoteTerminalTransport
{
    private readonly Func<int, long, CancellationToken, IAsyncEnumerable<RemoteTerminalChunk>> _script;

    /// <summary>Creates the transport.</summary>
    /// <param name="script">Receives the attach number (1 for the first), the <c>afterSequence</c> asked for and the token, and returns the stream.</param>
    internal ScriptedTerminalTransport(Func<int, long, CancellationToken, IAsyncEnumerable<RemoteTerminalChunk>> script)
    {
        _script = script;
    }

    /// <summary>Gets what the view asked for, one entry per attach: the terminal and the sequence it resumed after.</summary>
    internal List<(string TerminalId, long AfterSequence)> Attaches { get; } = [];

    /// <inheritdoc />
    public IAsyncEnumerable<RemoteTerminalChunk> AttachAsync(string terminalId, long afterSequence, CancellationToken cancellationToken)
    {
        Attaches.Add((terminalId, afterSequence));
        return _script(Attaches.Count, afterSequence, cancellationToken);
    }

    /// <summary>Builds a stream that delivers the chunks, then throws <paramref name="thenThrow"/>, completes, or stays open until cancelled.</summary>
    /// <param name="chunks">The chunks, as sequence and text.</param>
    /// <param name="thenThrow">The exception to throw after the chunks, or null.</param>
    /// <param name="hold">True to keep the stream open after the chunks (and when nothing is thrown).</param>
    /// <param name="cancellationToken">Ends a held stream.</param>
    /// <returns>The stream.</returns>
    internal static async IAsyncEnumerable<RemoteTerminalChunk> StreamAsync(
        IEnumerable<(long Sequence, string Text)> chunks,
        Exception? thenThrow = null,
        bool hold = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var (sequence, text) in chunks)
        {
            yield return new RemoteTerminalChunk(sequence, text);
        }

        if (thenThrow is not null)
        {
            throw thenThrow;
        }

        if (hold)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
    }
}
