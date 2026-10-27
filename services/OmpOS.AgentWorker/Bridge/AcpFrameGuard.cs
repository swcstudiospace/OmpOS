using System.Text.Json;
using System.Text.Unicode;

namespace OmpOS.AgentWorker.Bridge;

/// <summary>
/// Admits a WebSocket text frame to the line-delimited stdio protocol of <c>omp acp</c>. Each frame must be one JSON object. The stdio
/// framing is one object per line, so line breaks inside the text are removed: in valid JSON they can only be whitespace between
/// tokens, because a string may not contain a raw line break.
/// </summary>
internal static class AcpFrameGuard
{
    /// <summary>Validates a frame and removes line breaks from it.</summary>
    /// <param name="text">The UTF-8 bytes of the text frame.</param>
    /// <param name="frame">The single-line JSON object bytes when the frame is acceptable; empty otherwise.</param>
    /// <returns><see langword="true"/> when the text is exactly one JSON object.</returns>
    public static bool TryNormalize(ReadOnlySpan<byte> text, out byte[] frame)
    {
        frame = [];
        if (text.IsEmpty || !Utf8.IsValid(text) || !IsJsonObject(text))
        {
            return false;
        }

        if (text.IndexOfAny((byte)'\n', (byte)'\r') < 0)
        {
            frame = text.ToArray();
            return true;
        }

        var buffer = new List<byte>(text.Length);
        foreach (var value in text)
        {
            if (value is not ((byte)'\n' or (byte)'\r'))
            {
                buffer.Add(value);
            }
        }

        frame = [.. buffer];
        return true;
    }

    private static bool IsJsonObject(ReadOnlySpan<byte> text)
    {
        var reader = new Utf8JsonReader(text, isFinalBlock: true, state: default);
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }

            while (reader.Read())
            {
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return true;
    }
}
