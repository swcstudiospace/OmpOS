using System.Text;

namespace OmpOS.Presentation.Mentions;

/// <summary>Cuts text to a byte cap without splitting a character.</summary>
internal static class MentionText
{
    /// <summary>Cuts <paramref name="text"/> to at most <paramref name="maxBytes"/> UTF-8 bytes on a character boundary and reports the full size.</summary>
    /// <param name="text">The full text.</param>
    /// <param name="maxBytes">The byte cap; zero or negative keeps nothing.</param>
    /// <param name="keepEnd">Whether to keep the end of the text (terminal output, transcripts) instead of its start.</param>
    /// <returns>The cut text with the full size and the truncated flag.</returns>
    internal static MentionContent Cut(string text, int maxBytes, bool keepEnd)
    {
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
        if (!keepEnd)
        {
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

        var start = text.Length;
        while (start > 0)
        {
            var width = char.IsLowSurrogate(text[start - 1]) && start - 2 >= 0 ? 2 : 1;
            var cost = Encoding.UTF8.GetByteCount(text.AsSpan(start - width, width));
            if (bytes + cost > maxBytes)
            {
                break;
            }

            bytes += cost;
            start -= width;
        }

        return new MentionContent(text[start..], full, true);
    }
}
