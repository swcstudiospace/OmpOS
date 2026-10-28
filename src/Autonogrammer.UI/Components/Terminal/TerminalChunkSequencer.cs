using Autonogrammer.Presentation.Terminal;

namespace Autonogrammer.UI.Components.Terminal;

/// <summary>
/// Decides which chunks of a remote terminal stream reach the screen. The transport delivers at least once, so a chunk at or before the
/// last accepted sequence is a duplicate and is dropped; a chunk that skips ahead means output was lost, so it is refused and the stream
/// is attached again from <see cref="Acked"/>. The same rules as <c>TerminalSurfaceModel</c>, without keeping the whole output.
/// </summary>
public sealed class TerminalChunkSequencer
{
    /// <summary>Gets the highest sequence accepted so far; 0 before the first chunk. A reconnect asks for the chunks after it.</summary>
    public long Acked { get; private set; }

    /// <summary>Classifies a chunk and, when it is the next one, accepts it.</summary>
    /// <param name="chunk">The chunk the transport delivered.</param>
    /// <returns>The decision; only <see cref="TerminalChunkOutcome.Accepted"/> advances <see cref="Acked"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chunk"/> is null.</exception>
    public TerminalChunkOutcome Accept(RemoteTerminalChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (chunk.Sequence <= Acked)
        {
            return TerminalChunkOutcome.Duplicate;
        }

        if (chunk.Sequence != Acked + 1)
        {
            return TerminalChunkOutcome.Gap;
        }

        Acked = chunk.Sequence;
        return TerminalChunkOutcome.Accepted;
    }
}
