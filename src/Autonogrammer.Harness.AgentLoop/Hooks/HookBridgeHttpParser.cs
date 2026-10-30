using System.Buffers;
using System.Text;

namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>
/// A strict reader for the head of an HTTP/1.1 request, just enough for the bridge. It accepts only what a well-behaved client
/// sends and rejects everything that could be read two ways: bare CR or LF, control and non-ASCII bytes, obsolete line
/// folding, whitespace before a header colon, a request line that is not exactly <c>METHOD SP target SP HTTP/1.1</c>, an
/// absolute-form target, and a repeated <c>Content-Length</c> or <c>Authorization</c> header. It keeps only the method, the
/// target, the authorization, the content length and whether a <c>Transfer-Encoding</c> is present.
/// </summary>
internal static class HookBridgeHttpParser
{
    /// <summary>The most header lines a head may have.</summary>
    internal const int MaxHeaderLines = 100;

    private const int MaxMethodLength = 16;
    private const int MaxDecimalDigits = 18;

    private static readonly SearchValues<byte> TokenBytes =
        SearchValues.Create("!#$%&'*+-.^_`|~0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"u8);

    // Printable ASCII (0x20 to 0x7E) and the horizontal tab: everything else, including CR, LF, NUL and any byte of 0x80 or
    // more, makes a line invalid.
    private static readonly SearchValues<byte> LineBytes = SearchValues.Create(AllowedLineBytes());

    private static ReadOnlySpan<byte> Terminator => "\r\n\r\n"u8;

    private static ReadOnlySpan<byte> LineBreak => "\r\n"u8;

    private static ReadOnlySpan<byte> Version => "HTTP/1.1"u8;

    private static ReadOnlySpan<byte> Whitespace => " \t"u8;

    /// <summary>Parses a request head.</summary>
    /// <param name="head">The head including its terminating blank line, <c>CRLF CRLF</c>.</param>
    /// <param name="parsed">The parsed head when the result is <see cref="HookBridgeHttpResponse.Ok"/>, otherwise null.</param>
    /// <returns>
    /// <see cref="HookBridgeHttpResponse.Ok"/> when the head is valid, <see cref="HookBridgeHttpResponse.HeadersTooLarge"/> when it
    /// has more than <see cref="MaxHeaderLines"/> header lines, and <see cref="HookBridgeHttpResponse.BadRequest"/> otherwise.
    /// </returns>
    internal static int Parse(ReadOnlySpan<byte> head, out HookBridgeHttpHead? parsed)
    {
        parsed = null;
        if (head.Length < Terminator.Length || !head[^Terminator.Length..].SequenceEqual(Terminator))
        {
            return HookBridgeHttpResponse.BadRequest;
        }

        var rest = head[..^Terminator.Length];
        if (!TryParseRequestLine(NextLine(ref rest), out var method, out var target))
        {
            return HookBridgeHttpResponse.BadRequest;
        }

        var fields = new HeaderFields();
        var lines = 0;
        while (!rest.IsEmpty)
        {
            var line = NextLine(ref rest);
            if (++lines > MaxHeaderLines)
            {
                return HookBridgeHttpResponse.HeadersTooLarge;
            }

            var colon = IndexOfColon(line);
            if (colon < 0 || !TryApplyHeader(line[..colon], line[(colon + 1)..].Trim(Whitespace), fields))
            {
                return HookBridgeHttpResponse.BadRequest;
            }
        }

        parsed = new HookBridgeHttpHead
        {
            Method = method,
            Target = target,
            Authorization = fields.Authorization,
            ContentLength = fields.ContentLength,
            HasTransferEncoding = fields.HasTransferEncoding,
        };
        return HookBridgeHttpResponse.Ok;
    }

    // Records the headers the bridge acts on. Returns false for a header that must not appear twice or has an invalid value.
    private static bool TryApplyHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value, HeaderFields fields)
    {
        if (Ascii.EqualsIgnoreCase(name, "content-length"u8))
        {
            if (fields.ContentLength is not null || !TryParseContentLength(value, out var length))
            {
                return false;
            }

            fields.ContentLength = length;
        }
        else if (Ascii.EqualsIgnoreCase(name, "authorization"u8))
        {
            if (fields.Authorization is not null)
            {
                return false;
            }

            fields.Authorization = Encoding.ASCII.GetString(value);
        }
        else if (Ascii.EqualsIgnoreCase(name, "transfer-encoding"u8))
        {
            fields.HasTransferEncoding = true;
        }

        return true;
    }

    private static ReadOnlySpan<byte> NextLine(ref ReadOnlySpan<byte> rest)
    {
        var end = rest.IndexOf(LineBreak);
        if (end < 0)
        {
            var last = rest;
            rest = default;
            return last;
        }

        var line = rest[..end];
        rest = rest[(end + LineBreak.Length)..];
        return line;
    }

    // Returns the index of the colon that ends a valid header name, or -1 when the line is not a valid header line: it is
    // empty, it starts with whitespace (obsolete folding), it holds a byte that is not printable ASCII or a tab, it has no
    // colon, or the name is empty or not a token (which rejects whitespace before the colon).
    private static int IndexOfColon(ReadOnlySpan<byte> line)
    {
        if (!IsPrintableLine(line) || line[0] is (byte)' ' or (byte)'\t')
        {
            return -1;
        }

        var colon = line.IndexOf((byte)':');
        return colon > 0 && line[..colon].IndexOfAnyExcept(TokenBytes) < 0 ? colon : -1;
    }

    private static bool IsPrintableLine(ReadOnlySpan<byte> line) =>
        !line.IsEmpty && line.IndexOfAnyExcept(LineBytes) < 0;

    private static byte[] AllowedLineBytes()
    {
        const byte firstPrintable = 0x20;
        const byte lastPrintable = 0x7E;
        var allowed = new byte[lastPrintable - firstPrintable + 2];
        for (var index = 0; index <= lastPrintable - firstPrintable; index++)
        {
            allowed[index] = (byte)(firstPrintable + index);
        }

        allowed[^1] = (byte)'\t';
        return allowed;
    }

    private static bool TryParseRequestLine(ReadOnlySpan<byte> line, out string method, out string target)
    {
        method = string.Empty;
        target = string.Empty;
        if (!IsPrintableLine(line))
        {
            return false;
        }

        var firstSpace = line.IndexOf((byte)' ');
        if (firstSpace is <= 0 or > MaxMethodLength || line[..firstSpace].IndexOfAnyExcept(TokenBytes) >= 0)
        {
            return false;
        }

        var afterMethod = line[(firstSpace + 1)..];
        var secondSpace = afterMethod.IndexOf((byte)' ');
        if (secondSpace <= 0 || !afterMethod[(secondSpace + 1)..].SequenceEqual(Version))
        {
            return false;
        }

        var targetBytes = afterMethod[..secondSpace];
        if (targetBytes[0] != (byte)'/' || targetBytes.Contains((byte)'#') || targetBytes.Contains((byte)'\t'))
        {
            return false;
        }

        method = Encoding.ASCII.GetString(line[..firstSpace]);
        target = Encoding.ASCII.GetString(targetBytes);
        return true;
    }

    private static bool TryParseContentLength(ReadOnlySpan<byte> value, out long length)
    {
        length = 0;
        if (value.IsEmpty || value.IndexOfAnyExceptInRange((byte)'0', (byte)'9') >= 0)
        {
            return false;
        }

        if (value.Length > MaxDecimalDigits)
        {
            length = long.MaxValue;
            return true;
        }

        foreach (var digit in value)
        {
            length = (length * 10) + (digit - (byte)'0');
        }

        return true;
    }

    private sealed class HeaderFields
    {
        public string? Authorization { get; set; }

        public long? ContentLength { get; set; }

        public bool HasTransferEncoding { get; set; }
    }
}
