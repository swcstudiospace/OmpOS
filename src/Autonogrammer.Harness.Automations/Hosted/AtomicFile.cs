namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// Writes a file so that a crash leaves either the old content or the new: the bytes go to a temporary file in the same directory that
/// only the owner can access, are flushed to disk, and replace the target by rename.
/// </summary>
internal static class AtomicFile
{
    private const UnixFileMode OwnerDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>Replaces a file's content atomically, creating its directory (owner-only on Unix) when needed.</summary>
    /// <param name="path">The full path of the target file.</param>
    /// <param name="bytes">The new content.</param>
    /// <param name="cancellationToken">Cancels the write before the rename.</param>
    /// <returns>A task that completes when the file holds the new content.</returns>
    public static async ValueTask WriteAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            if (OperatingSystem.IsWindows())
            {
                _ = Directory.CreateDirectory(directory);
            }
            else
            {
                _ = Directory.CreateDirectory(directory, OwnerDirectoryMode);
            }
        }

        var temporary = $"{path}.{Guid.NewGuid().ToString("N")}.tmp";
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = OwnerFileMode;
            }

            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                RandomAccess.FlushToDisk(stream.SafeFileHandle);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
