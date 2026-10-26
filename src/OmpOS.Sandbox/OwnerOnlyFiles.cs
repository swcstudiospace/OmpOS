using System.Text;

namespace OmpOS.Sandbox;

/// <summary>
/// Creates files and directories that only the current user can read, with the permissions applied at creation so no
/// other user can open the entry between creation and a later chmod. On Windows the user profile ACLs of the temp
/// directory apply and no Unix mode is set.
/// </summary>
internal static class OwnerOnlyFiles
{
    private const UnixFileMode OwnerDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Creates a directory that must not exist yet, with mode 0700 on Unix. The parent directory is created with default
    /// permissions when it is missing.
    /// </summary>
    /// <param name="path">The directory to create.</param>
    /// <exception cref="IOException">The directory already exists, so it may not be owned by the current user.</exception>
    public static void CreateDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Directory.Exists(path))
        {
            throw new IOException($"The directory '{path}' already exists and is not created by this process.");
        }

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            return;
        }

        Directory.CreateDirectory(path, OwnerDirectoryMode);
    }

    /// <summary>
    /// Creates a file that must not exist yet, with mode 0600 on Unix, and writes UTF-8 text without a byte order mark.
    /// </summary>
    /// <param name="path">The file to create.</param>
    /// <param name="content">The text to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public static async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        var options = new FileStreamOptions
        {
            Mode = System.IO.FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerFileMode;
        }

        var stream = new FileStream(path, options);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(Utf8WithoutBom.GetBytes(content), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
