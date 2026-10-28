using Avalonia.Platform.Storage;

namespace OmpOS.Native.Views.Attachments;

/// <summary>Reads a storage file into a <see cref="PickedImage"/>, never more than one byte past the limit.</summary>
internal static class StorageImageReader
{
    /// <summary>Reads one file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="maxBytes">The size limit.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file's name and bytes.</returns>
    internal static async Task<PickedImage> ReadAsync(IStorageFile file, long maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        var stream = await file.OpenReadAsync().ConfigureAwait(true);
        await using (stream.ConfigureAwait(true))
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            while (buffer.Length <= maxBytes)
            {
                var want = (int)Math.Min(chunk.Length, maxBytes + 1 - buffer.Length);
                var read = await stream.ReadAsync(chunk.AsMemory(0, want), cancellationToken).ConfigureAwait(true);
                if (read == 0)
                {
                    break;
                }

                buffer.Write(chunk, 0, read);
            }

            return new PickedImage(file.Name, buffer.ToArray(), null);
        }
    }
}
