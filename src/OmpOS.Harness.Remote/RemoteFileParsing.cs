using System.Globalization;
using System.Text;

namespace OmpOS.Harness.Remote;

/// <summary>Reads the answers of the scripts in <see cref="RemoteScripts"/>: signed file reads, directory listings and stat lines, and decodes file bytes as strict UTF-8 text.</summary>
internal static class RemoteFileParsing
{
    /// <summary>The number of leading bytes searched for a NUL byte, which marks a binary file.</summary>
    public const int BinarySniffBytes = 8000;

    /// <summary>The room the signature line needs in the output cap, on top of the content.</summary>
    public const int SignatureRoom = 256;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Gets the encoding used to turn text into the bytes written to a remote file: UTF-8 without a byte order mark that rejects a lone surrogate.</summary>
    public static UTF8Encoding Utf8 => StrictUtf8;

    /// <summary>Splits the output of <see cref="RemoteScripts.ReadWithSignature"/> into the file signature and the content bytes.</summary>
    /// <param name="output">The output bytes.</param>
    /// <param name="signature">The signature, or an empty string when the marker was not found.</param>
    /// <param name="content">The content bytes after the signature line.</param>
    /// <returns><see langword="true"/> when the signature line was found.</returns>
    public static bool TrySplitSignedRead(ReadOnlyMemory<byte> output, out string signature, out ReadOnlyMemory<byte> content)
    {
        signature = string.Empty;
        content = ReadOnlyMemory<byte>.Empty;
        var span = output.Span;
        var marker = Utf8.GetBytes(RemoteScripts.SignatureMarker);
        var from = 0;
        while (from < span.Length)
        {
            var found = span[from..].IndexOf(marker);
            if (found < 0)
            {
                return false;
            }

            var start = from + found;
            if (start == 0 || span[start - 1] == (byte)'\n')
            {
                var lineEnd = span[start..].IndexOf((byte)'\n');
                if (lineEnd < 0)
                {
                    return false;
                }

                signature = Utf8.GetString(span.Slice(start + marker.Length, lineEnd - marker.Length)).Trim();
                content = output[(start + lineEnd + 1)..];
                return true;
            }

            from = start + marker.Length;
        }

        return false;
    }

    /// <summary>Decodes file bytes as UTF-8 text without the byte order mark.</summary>
    /// <param name="bytes">The file bytes.</param>
    /// <param name="hasByteOrderMark">Whether the bytes started with a UTF-8 byte order mark.</param>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidDataException">The bytes hold a NUL in their first 8000 bytes (a binary file) or are not valid UTF-8.</exception>
    public static string DecodeText(ReadOnlySpan<byte> bytes, out bool hasByteOrderMark)
    {
        hasByteOrderMark = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var body = hasByteOrderMark ? bytes[3..] : bytes;
        if (body[..Math.Min(body.Length, BinarySniffBytes)].Contains((byte)0))
        {
            throw new InvalidDataException("The file is binary, not text.");
        }

        try
        {
            return StrictUtf8.GetString(body);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("The file is not valid UTF-8 text.", exception);
        }
    }

    /// <summary>Reads the lines of a listing made by <see cref="RemoteScripts.List"/>.</summary>
    /// <param name="output">The output text.</param>
    /// <returns>The entries, and whether the directory held more entries than the cap.</returns>
    public static (List<(string Name, bool IsDirectory, bool IsLink)> Entries, bool Truncated) ParseList(string output)
    {
        var entries = new List<(string Name, bool IsDirectory, bool IsLink)>();
        var truncated = false;
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.Equals(line, "T", StringComparison.Ordinal))
            {
                truncated = true;
                continue;
            }

            if (line.Length < 4 || line[0] is not ('D' or 'F') || line[1] is not ('L' or '.') || line[2] != ' ')
            {
                continue;
            }

            var name = line[3..];
            if (!name.Contains('\uFFFD', StringComparison.Ordinal))
            {
                entries.Add((name, line[0] == 'D', line[1] == 'L'));
            }
        }

        return (entries, truncated);
    }

    /// <summary>Reads the answer of <see cref="RemoteScripts.Stat"/>.</summary>
    /// <param name="output">The output text.</param>
    /// <param name="kind">The kind: <c>missing</c>, <c>dir</c>, <c>file</c> or <c>other</c>; <see langword="null"/> when no answer line was found.</param>
    /// <param name="length">The size in bytes, when printed.</param>
    /// <param name="modified">The modification time, when printed.</param>
    /// <returns><see langword="true"/> when an answer line was found.</returns>
    public static bool TryParseStat(string output, out string? kind, out long? length, out DateTimeOffset? modified)
    {
        kind = null;
        length = null;
        modified = null;
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (!line.StartsWith(RemoteScripts.StatMarker, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var part in line[RemoteScripts.StatMarker.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                ApplyStatField(part, ref kind, ref length, ref modified);
            }

            return kind is not null;
        }

        return false;
    }

    private static void ApplyStatField(string part, ref string? kind, ref long? length, ref DateTimeOffset? modified)
    {
        var separator = part.IndexOf('=', StringComparison.Ordinal);
        if (separator <= 0)
        {
            return;
        }

        var name = part[..separator];
        var value = part[(separator + 1)..];
        if (string.Equals(name, "kind", StringComparison.Ordinal))
        {
            kind = value;
        }
        else if (string.Equals(name, "size", StringComparison.Ordinal) && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var size))
        {
            length = size;
        }
        else if (string.Equals(name, "mtime", StringComparison.Ordinal) && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            modified = DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
    }
}
