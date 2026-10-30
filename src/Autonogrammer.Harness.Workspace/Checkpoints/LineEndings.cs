namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>Makes stored text fit the file it goes back into: the file's line endings and byte order mark are kept.</summary>
internal static class LineEndings
{
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>Adapts the stored content of a file to the line endings and byte order mark of the file's current content.</summary>
    /// <param name="stored">The content to write.</param>
    /// <param name="current">The content the file holds now, or an empty span when the file does not exist.</param>
    /// <returns>The bytes to write: <paramref name="stored"/> itself unless the file uses CRLF and the stored text uses bare LF, or the file has a byte order mark that the stored text lacks.</returns>
    public static byte[] Adapt(ReadOnlySpan<byte> stored, ReadOnlySpan<byte> current)
    {
        if (CheckpointHash.IsBinary(stored) || current.IsEmpty || CheckpointHash.IsBinary(current))
        {
            return stored.ToArray();
        }

        var result = stored.ToArray();
        if (UsesCrlf(current) && !stored.Contains((byte)'\r'))
        {
            result = ToCrlf(result);
        }

        if (current.StartsWith(Bom) && !result.AsSpan().StartsWith(Bom))
        {
            result = [.. Bom, .. result];
        }

        return result;
    }

    private static bool UsesCrlf(ReadOnlySpan<byte> content)
    {
        var crlf = 0;
        var lf = 0;
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] != (byte)'\n')
            {
                continue;
            }

            if (index > 0 && content[index - 1] == (byte)'\r')
            {
                crlf++;
            }
            else
            {
                lf++;
            }
        }

        return crlf > lf;
    }

    private static byte[] ToCrlf(byte[] text)
    {
        var result = new List<byte>(text.Length + (text.Length / 16));
        foreach (var value in text)
        {
            if (value == (byte)'\n')
            {
                result.Add((byte)'\r');
            }

            result.Add(value);
        }

        return [.. result];
    }
}
