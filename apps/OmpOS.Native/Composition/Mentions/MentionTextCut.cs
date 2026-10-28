using System.Text;
using OmpOS.Presentation.Mentions;

namespace OmpOS.Native.Composition.Mentions;

/// <summary>Cuts text to a byte budget on a character boundary, for the mention adapters that read files, diffs and rule bodies.</summary>
internal static class MentionTextCut
{
    /// <summary>Cuts a string to at most <paramref name="maxBytes"/> UTF-8 bytes, keeping the start and never splitting a surrogate pair.</summary>
    /// <param name="text">The full text.</param>
    /// <param name="maxBytes">The byte cap; a cap of zero or less keeps nothing.</param>
    /// <returns>The kept text, the UTF-8 size of the full text and whether anything was cut.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    internal static MentionContent Cut(string text, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(text);

        var full = Encoding.UTF8.GetByteCount(text);
        if (full <= maxBytes)
        {
            return new MentionContent(text, full, false);
        }

        if (maxBytes <= 0)
        {
            return new MentionContent(string.Empty, full, true);
        }

        var bytes = 0;
        var end = 0;
        while (end < text.Length)
        {
            var width = char.IsHighSurrogate(text[end]) && end + 1 < text.Length ? 2 : 1;
            var cost = Encoding.UTF8.GetByteCount(text.AsSpan(end, width));
            if (bytes + cost > maxBytes)
            {
                break;
            }

            bytes += cost;
            end += width;
        }

        return new MentionContent(text[..end], full, true);
    }

    /// <summary>
    /// Decodes the start of a UTF-8 byte sequence that may have been cut in the middle of a character. The bytes of an incomplete last character are dropped, so the
    /// text ends on a character boundary; a leading byte order mark is dropped, and a byte that is not valid UTF-8 reads as the replacement character.
    /// </summary>
    /// <param name="bytes">The bytes, cut at any position.</param>
    /// <returns>The text.</returns>
    internal static string DecodeHead(ReadOnlySpan<byte> bytes)
    {
        var decoder = Encoding.UTF8.GetDecoder();
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        var count = decoder.GetChars(bytes, chars, flush: false);
        var start = count > 0 && chars[0] == '\uFEFF' ? 1 : 0;
        return new string(chars, start, count - start);
    }
}
