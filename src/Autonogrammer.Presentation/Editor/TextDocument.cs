using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// An editable text buffer with a line index, the model behind one editor tab (ADE-09). The text is held as a string; the
/// only way to change it is <see cref="Replace"/>, so the line index always matches the text.
/// </summary>
/// <remarks>
/// A document always has at least one line: the empty text has one empty line, and text ending in <c>'\n'</c> has a final
/// empty line after it, as editors show it.
/// </remarks>
public sealed class TextDocument : ObservableModel
{
    private string _text;
    private int[] _lineStarts;

    /// <summary>Creates a document holding <paramref name="text"/>.</summary>
    /// <param name="text">The initial text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public TextDocument(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _text = text;
        _lineStarts = IndexLines(text);
    }

    /// <summary>Gets the full text.</summary>
    public string Text => _text;

    /// <summary>Gets the length of the text in characters.</summary>
    public int Length => _text.Length;

    /// <summary>Gets the number of lines, always at least one.</summary>
    public int LineCount => _lineStarts.Length;

    /// <summary>Gets the text of one line, without its <c>'\n'</c>.</summary>
    /// <param name="line">The zero-based line index.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="line"/> is outside the document.</exception>
    public string GetLine(int line)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(line, LineCount);

        var start = _lineStarts[line];
        var end = LineEnd(line);
        return _text.Substring(start, end - start);
    }

    /// <summary>Gets the line and column of a character offset. The offset equal to <see cref="Length"/> names the end of the text.</summary>
    /// <param name="offset">A character offset from zero to <see cref="Length"/>, inclusive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is outside the text.</exception>
    public TextPosition GetPosition(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, _text.Length);

        // The last line start at or before the offset: a binary search over the sorted line starts.
        var low = 0;
        var high = _lineStarts.Length - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (_lineStarts[middle] <= offset)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return new TextPosition(low, offset - _lineStarts[low]);
    }

    /// <summary>Gets the character offset of a line and column.</summary>
    /// <param name="line">The zero-based line index.</param>
    /// <param name="column">The zero-based column. It may equal the line length, which names the position before the line break.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="line"/> or <paramref name="column"/> is outside the document.</exception>
    public int GetOffset(int line, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(line, LineCount);
        ArgumentOutOfRangeException.ThrowIfNegative(column);

        var lineStart = _lineStarts[line];
        if (column > LineEnd(line) - lineStart)
        {
            throw new ArgumentOutOfRangeException(nameof(column), "The column is past the end of the line.");
        }

        return lineStart + column;
    }

    /// <summary>Gets the text in a range.</summary>
    /// <param name="start">The first character offset.</param>
    /// <param name="length">The number of characters.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range is outside the text.</exception>
    public string GetRange(int start, int length)
    {
        ValidateRange(start, length);
        return _text.Substring(start, length);
    }

    /// <summary>Replaces a range of characters. The line index is rebuilt, and <see cref="Text"/> is announced only when the text changes.</summary>
    /// <param name="start">The first character offset of the range.</param>
    /// <param name="length">The number of characters to replace. Zero inserts.</param>
    /// <param name="replacement">The text that takes the range's place.</param>
    /// <exception cref="ArgumentNullException"><paramref name="replacement"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The range is outside the text.</exception>
    public void Replace(int start, int length, string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ValidateRange(start, length);

        var updated = string.Concat(_text.AsSpan(0, start), replacement, _text.AsSpan(start + length));
        if (string.Equals(updated, _text, StringComparison.Ordinal))
        {
            return;
        }

        _text = updated;
        _lineStarts = IndexLines(updated);
        NotifyPropertyChanged(nameof(Text));
        NotifyPropertyChanged(nameof(Length));
        NotifyPropertyChanged(nameof(LineCount));
    }

    // The offset of the line break that ends a line, or the text length for the last line.
    private int LineEnd(int line) => line + 1 < _lineStarts.Length ? _lineStarts[line + 1] - 1 : _text.Length;

    private void ValidateRange(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, _text.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, _text.Length - start);
    }

    private static int[] IndexLines(string text)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                starts.Add(index + 1);
            }
        }

        return [.. starts];
    }
}
