using System.Text;

namespace Autonogrammer.Web.Services;

/// <summary>
/// Incremental server-sent-events frame parser. Chunks may split frames at any byte:
/// <see cref="Feed"/> buffers the trailing partial line and returns only complete frames.
/// A blank line dispatches the pending event; lines starting with a colon are comments
/// (heartbeats) and are ignored.
/// </summary>
internal sealed class SseParser
{
    /// <summary>The longest accepted SSE line, in characters, including a partial line still buffered.</summary>
    internal const int MaxLineLength = 1024 * 1024;

    /// <summary>The largest accepted SSE frame data, in characters, before its blank line dispatches it.</summary>
    internal const int MaxFrameLength = 4 * 1024 * 1024;

    private readonly StringBuilder _lineBuffer = new();
    private readonly List<string> _dataLines = new();
    private string? _pendingId;
    private string? _pendingEventType;
    private int _frameLength;

    /// <summary>
    /// Feeds one text chunk and returns every frame completed by it.
    /// </summary>
    /// <param name="chunk">The next chunk of the response body.</param>
    /// <returns>The frames whose terminating blank line arrived in this chunk.</returns>
    /// <exception cref="ApiClientException">A line or the pending frame data exceeds its length limit.</exception>
    internal IReadOnlyList<SseFrame> Feed(string chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        var frames = new List<SseFrame>();
        var start = 0;
        for (var i = 0; i < chunk.Length; i++)
        {
            if (chunk[i] == '\n')
            {
                EnsureLineWithinLimit(_lineBuffer.Length + (i - start));
                _lineBuffer.Append(chunk, start, i - start);
                start = i + 1;
                var frame = ConsumeLine(_lineBuffer.ToString());
                _lineBuffer.Clear();
                if (frame is not null)
                {
                    frames.Add(frame);
                }
            }
        }

        EnsureLineWithinLimit(_lineBuffer.Length + (chunk.Length - start));
        _lineBuffer.Append(chunk, start, chunk.Length - start);
        return frames;
    }

    /// <summary>
    /// Dispatches the pending event at end-of-stream, when the body ends without a blank line.
    /// </summary>
    /// <returns>The trailing frame, or null when nothing is pending.</returns>
    internal SseFrame? Flush()
    {
        if (_lineBuffer.Length > 0)
        {
            var frame = ConsumeLine(_lineBuffer.ToString());
            _lineBuffer.Clear();
            if (frame is not null)
            {
                return frame;
            }
        }

        if (_dataLines.Count == 0 && _pendingId is null && _pendingEventType is null)
        {
            return null;
        }

        return Dispatch();
    }

    private SseFrame? ConsumeLine(string rawLine)
    {
        var line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;
        if (line.Length == 0)
        {
            return _dataLines.Count == 0 && _pendingId is null && _pendingEventType is null
                ? null
                : Dispatch();
        }

        if (line[0] == ':')
        {
            return null;
        }

        string field;
        string value;
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            field = line;
            value = string.Empty;
        }
        else
        {
            field = line[..colon];
            value = line[(colon + 1)..];
            if (value.StartsWith(' '))
            {
                value = value[1..];
            }
        }

        if (string.Equals(field, "data", StringComparison.Ordinal))
        {
            AppendData(value);
        }
        else if (string.Equals(field, "event", StringComparison.Ordinal))
        {
            _pendingEventType = value;
        }
        else if (string.Equals(field, "id", StringComparison.Ordinal))
        {
            _pendingId = value;
        }

        return null;
    }

    private SseFrame Dispatch()
    {
        var frame = new SseFrame(_pendingId, _pendingEventType, string.Join('\n', _dataLines));
        _dataLines.Clear();
        _frameLength = 0;
        _pendingId = null;
        _pendingEventType = null;
        return frame;
    }

    private void AppendData(string value)
    {
        // Data lines join with '\n', so every line after the first adds one separator.
        _frameLength += value.Length + (_dataLines.Count == 0 ? 0 : 1);
        if (_frameLength > MaxFrameLength)
        {
            throw new ApiClientException($"An SSE frame's data exceeds the {MaxFrameLength}-character limit.");
        }

        _dataLines.Add(value);
    }

    private static void EnsureLineWithinLimit(int length)
    {
        if (length > MaxLineLength)
        {
            throw new ApiClientException($"An SSE line exceeds the {MaxLineLength}-character limit.");
        }
    }
}
