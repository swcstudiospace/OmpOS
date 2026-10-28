namespace OmpOS.Workspace.Checkpoints;

/// <summary>File helpers of the checkpoint store: owner-only directories and files, and writes that replace a file at once.</summary>
internal static class CheckpointFiles
{
    private const UnixFileMode OwnerDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>Creates a directory that only its owner can use (mode 0700 on Unix systems).</summary>
    /// <param name="path">The directory; missing parents are created the same way.</param>
    public static void CreateOwnerDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            // The runtime applies the mode to the last directory only, so every missing parent is created in turn.
            var full = Path.GetFullPath(path);
            if (Path.GetDirectoryName(full) is { Length: > 0 } parent && !Directory.Exists(parent))
            {
                CreateOwnerDirectory(parent);
            }

            if (!Directory.Exists(full))
            {
                Directory.CreateDirectory(full, OwnerDirectory);
            }
        }
    }

    /// <summary>Writes a file that only its owner can read (mode 0600 on Unix systems), through a temporary file that then replaces it.</summary>
    /// <param name="path">The file. Its directory must exist.</param>
    /// <param name="content">The bytes.</param>
    /// <param name="cancellationToken">Cancels the write; the temporary file is removed.</param>
    public static Task WriteOwnerFileAsync(string path, byte[] content, CancellationToken cancellationToken) =>
        WriteAsync(path, content, OwnerFile, cancellationToken);

    /// <summary>
    /// Writes a workspace file through a temporary file in the same directory, which then replaces it, so a reader never sees a partial file.
    /// The permissions of a file that exists are kept.
    /// </summary>
    /// <param name="path">The file. Its directory is created when it is missing.</param>
    /// <param name="content">The bytes.</param>
    /// <param name="cancellationToken">Cancels the write; the temporary file is removed.</param>
    public static Task WriteWorkspaceFileAsync(string path, byte[] content, CancellationToken cancellationToken)
    {
        UnixFileMode? mode = !OperatingSystem.IsWindows() && File.Exists(path) ? File.GetUnixFileMode(path) : null;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return WriteAsync(path, content, mode, cancellationToken);
    }

    private static async Task WriteAsync(string path, byte[] content, UnixFileMode? mode, CancellationToken cancellationToken)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            };
            if (mode is { } unixMode && !OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = unixMode;
            }

            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
            }

            if (mode is { } finalMode && !OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporary, finalMode);
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
