namespace OmpOS.Harness.AgentLoop.Stages;

/// <summary>
/// Reads vendor state files without trusting their size. Every read opens the file shared for read and write, so the plugin
/// that owns the file is never blocked, and never reads more than the cap.
/// </summary>
internal static class StageFileReader
{
    /// <summary>Reads a whole file when it is no larger than <paramref name="maxBytes"/>.</summary>
    /// <param name="path">The file to read.</param>
    /// <param name="maxBytes">The size cap.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The result. Never throws for a missing, locked or oversize file.</returns>
    public static async Task<FileReadResult> ReadAllAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        try
        {
            var stream = Open(path);
            await using (stream.ConfigureAwait(false))
            {
                var length = stream.Length;
                if (length > maxBytes)
                {
                    return new FileReadResult(FileReadStatus.TooLarge, ReadOnlyMemory<byte>.Empty, null);
                }

                var buffer = new byte[(int)length];
                var total = 0;
                while (total < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    total += read;
                }

                if (total == buffer.Length && await HasMoreAsync(stream, cancellationToken).ConfigureAwait(false))
                {
                    return new FileReadResult(FileReadStatus.TooLarge, ReadOnlyMemory<byte>.Empty, null);
                }

                return new FileReadResult(FileReadStatus.Ok, buffer.AsMemory(0, total), null);
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new FileReadResult(FileReadStatus.Missing, ReadOnlyMemory<byte>.Empty, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new FileReadResult(FileReadStatus.Unreadable, ReadOnlyMemory<byte>.Empty, ex);
        }
    }

    /// <summary>Reads at most <paramref name="maxBytes"/> bytes of a file, starting at <paramref name="offset"/>.</summary>
    /// <param name="path">The file to read.</param>
    /// <param name="offset">The offset to start at. When the file is shorter than this, nothing is read and <see cref="RangeReadResult.FileLength"/> tells the caller.</param>
    /// <param name="maxBytes">The most bytes to read.</param>
    /// <param name="jumpToTailWhenFarBehind">When true and the file holds more than <paramref name="maxBytes"/> beyond <paramref name="offset"/>, the read starts <paramref name="maxBytes"/> before the end instead.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The result. Never throws for a missing or locked file.</returns>
    public static async Task<RangeReadResult> ReadRangeAsync(
        string path,
        long offset,
        int maxBytes,
        bool jumpToTailWhenFarBehind,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        try
        {
            var stream = Open(path);
            await using (stream.ConfigureAwait(false))
            {
                var length = stream.Length;
                if (length <= offset)
                {
                    return new RangeReadResult(FileReadStatus.Ok, length, offset, ReadOnlyMemory<byte>.Empty, false);
                }

                var start = offset;
                var jumped = false;
                if (jumpToTailWhenFarBehind && length - offset > maxBytes)
                {
                    start = length - maxBytes;
                    jumped = true;
                }

                var count = (int)Math.Min(maxBytes, length - start);
                stream.Seek(start, SeekOrigin.Begin);
                var buffer = new byte[count];
                var total = 0;
                while (total < count)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(total, count - total), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    total += read;
                }

                return new RangeReadResult(FileReadStatus.Ok, length, start, buffer.AsMemory(0, total), jumped);
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new RangeReadResult(FileReadStatus.Missing, 0, offset, ReadOnlyMemory<byte>.Empty, false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new RangeReadResult(FileReadStatus.Unreadable, 0, offset, ReadOnlyMemory<byte>.Empty, false);
        }
    }

    private static FileStream Open(string path) =>
        new(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.ReadWrite | FileShare.Delete,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            BufferSize = 0,
        });

    private static async Task<bool> HasMoreAsync(FileStream stream, CancellationToken cancellationToken)
    {
        var probe = new byte[1];
        return await stream.ReadAsync(probe, cancellationToken).ConfigureAwait(false) > 0;
    }
}
