using System.Text;

namespace OmpOS.Harness.Plugins;

/// <summary>
/// Bounds hook output to 16 KiB of UTF-8 per stream and never splits a character at the bound.
/// </summary>
public static class HookOutput
{
    /// <summary>
    /// The most UTF-8 bytes recorded for each of standard output and standard error of one hook run.
    /// </summary>
    public const int MaxBytes = 16 * 1024;

    /// <summary>
    /// Keeps at most <see cref="MaxBytes"/> UTF-8 bytes of <paramref name="text"/>, cutting only between characters.
    /// </summary>
    /// <param name="text">The output as a runner reported it.</param>
    /// <returns>The kept text, and whether any was dropped.</returns>
    public static (string Text, bool Truncated) Bound(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Encoding.UTF8.GetByteCount(text) <= MaxBytes)
        {
            return (text, false);
        }

        var bytes = 0;
        var end = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > MaxBytes)
            {
                break;
            }

            bytes += rune.Utf8SequenceLength;
            end += rune.Utf16SequenceLength;
        }

        return (text[..end], true);
    }

    /// <summary>
    /// Decodes captured UTF-8 bytes. When the capture was cut, a character split by the cut is dropped rather than
    /// shown as a replacement character.
    /// </summary>
    /// <param name="captured">The bytes the runner kept.</param>
    /// <param name="truncated">True when the capture was cut.</param>
    /// <returns>The decoded text.</returns>
    public static string Decode(byte[] captured, bool truncated)
    {
        ArgumentNullException.ThrowIfNull(captured);
        var length = truncated ? CompleteSequenceLength(captured) : captured.Length;
        return Encoding.UTF8.GetString(captured, 0, length);
    }

    private static int CompleteSequenceLength(byte[] bytes)
    {
        // Look back at most four bytes for the lead byte of a sequence that the cut may have split.
        var lookBack = Math.Min(4, bytes.Length);
        for (var back = 1; back <= lookBack; back++)
        {
            var b = bytes[bytes.Length - back];
            if ((b & 0xC0) == 0x80)
            {
                continue;
            }

            var expected = (b & 0x80) == 0 ? 1
                : (b & 0xE0) == 0xC0 ? 2
                : (b & 0xF0) == 0xE0 ? 3
                : (b & 0xF8) == 0xF0 ? 4
                : 1;
            return expected > back ? bytes.Length - back : bytes.Length;
        }

        return bytes.Length;
    }
}
