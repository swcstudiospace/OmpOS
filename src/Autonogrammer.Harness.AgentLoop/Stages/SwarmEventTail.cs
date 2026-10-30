namespace Autonogrammer.Harness.AgentLoop.Stages;

/// <summary>
/// Reads an append-only JSON-lines file incrementally. It remembers how far it has read, reads at most
/// <see cref="MaxChunkBytes"/> per poll, drops any line longer than <see cref="MaxLineBytes"/>, leaves a trailing line that has
/// no terminator yet for the next poll, and starts again from the beginning when the file becomes shorter than the offset. The
/// first poll of a file that holds more than one chunk starts one chunk before the end, so attaching to a long-running log
/// never replays all of its history.
/// </summary>
internal sealed class SwarmEventTail
{
    /// <summary>The most bytes read per poll.</summary>
    public const int MaxChunkBytes = 256 * 1024;

    /// <summary>The longest line that is passed on. A longer line is skipped.</summary>
    public const int MaxLineBytes = 64 * 1024;

    private long _offset;
    private bool _discarding;
    private bool _attached;

    /// <summary>Gets the offset of the first byte not yet consumed.</summary>
    public long Offset => _offset;

    /// <summary>Reads what was appended since the last poll.</summary>
    /// <param name="path">The file to tail.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The poll result. A missing or unreadable file yields no lines and keeps the offset.</returns>
    public async Task<TailPoll> PollAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var read = await StageFileReader.ReadRangeAsync(path, _offset, MaxChunkBytes, jumpToTailWhenFarBehind: !_attached, cancellationToken).ConfigureAwait(false);
            if (read.Status != FileReadStatus.Ok)
            {
                return new TailPoll(read.Status, []);
            }

            if (read.FileLength < _offset)
            {
                _offset = 0;
                _discarding = false;
                _attached = true;
                continue;
            }

            _attached = true;
            if (read.JumpedToTail)
            {
                _discarding = true;
            }

            return new TailPoll(FileReadStatus.Ok, Split(read.StartOffset, read.Content));
        }

        return new TailPoll(FileReadStatus.Ok, []);
    }

    private List<ReadOnlyMemory<byte>> Split(long startOffset, ReadOnlyMemory<byte> chunk)
    {
        var lines = new List<ReadOnlyMemory<byte>>();
        var span = chunk.Span;
        var position = 0;

        if (_discarding)
        {
            var terminator = span.IndexOf((byte)'\n');
            if (terminator < 0)
            {
                _offset = startOffset + span.Length;
                return lines;
            }

            _discarding = false;
            position = terminator + 1;
        }

        while (position < span.Length)
        {
            var relative = span[position..].IndexOf((byte)'\n');
            if (relative < 0)
            {
                if (span.Length - position >= MaxLineBytes)
                {
                    _discarding = true;
                    position = span.Length;
                }

                break;
            }

            var length = relative;
            if (length > 0 && span[position + length - 1] == (byte)'\r')
            {
                length--;
            }

            if (length > 0 && length <= MaxLineBytes)
            {
                lines.Add(chunk.Slice(position, length));
            }

            position += relative + 1;
        }

        _offset = startOffset + position;
        return lines;
    }
}
