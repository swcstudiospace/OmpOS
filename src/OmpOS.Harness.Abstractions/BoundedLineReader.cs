using System.Text;

namespace OmpOS.Harness.Abstractions;

/// <summary>
/// Reads one line from a text stream under a hard length cap. A line longer than <see cref="MaxLineLength"/>
/// characters is refused with <see cref="MalformedFrameHarnessException"/> instead of being buffered without
/// limit, so a misbehaving child or remote cannot exhaust memory.
/// </summary>
public static class BoundedLineReader
{
    /// <summary>The longest accepted line, in characters, excluding its terminator: the 1 MiB transport limit.</summary>
    public const int MaxLineLength = 1024 * 1024;

    /// <summary>
    /// Reads the next line and returns it without its terminator. A line ends at <c>\n</c>; a <c>\r</c> directly
    /// before it is removed, so <c>\r\n</c> and <c>\n</c> both end a line.
    /// </summary>
    /// <param name="reader">The source. It is read one character at a time so that nothing past the terminator is consumed.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The line, or null at the end of the stream. A final line without a terminator is returned as is.</returns>
    /// <exception cref="MalformedFrameHarnessException">The line exceeds <see cref="MaxLineLength"/> characters.</exception>
    public static async Task<string?> ReadLineAsync(TextReader reader, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var line = new StringBuilder();
        var character = new char[1];
        while (true)
        {
            var read = await reader.ReadAsync(character.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return line.Length == 0 ? null : TrimCarriageReturn(line);
            }

            if (character[0] == '\n')
            {
                return TrimCarriageReturn(line);
            }

            line.Append(character[0]);
            if (line.Length > MaxLineLength)
            {
                throw new MalformedFrameHarnessException("A physical frame exceeds the 1 MiB transport limit.");
            }
        }
    }

    private static string TrimCarriageReturn(StringBuilder line) =>
        line.Length > 0 && line[line.Length - 1] == '\r'
            ? line.ToString(0, line.Length - 1)
            : line.ToString();
}
