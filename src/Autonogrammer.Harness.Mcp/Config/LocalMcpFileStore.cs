using System.Text;

namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>Reads and writes configuration files on the local disk. A write goes to a temporary file in the same directory and replaces the file by rename.</summary>
public sealed class LocalMcpFileStore : IMcpFileStore
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public async ValueTask<McpFileRead> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        if (!File.Exists(path))
        {
            return McpFileRead.Missing;
        }

        try
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
            await using (stream.ConfigureAwait(false))
            {
                if (stream.Length > maxBytes)
                {
                    return McpFileRead.TooLarge;
                }

                var buffer = new byte[(int)stream.Length];
                await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
                return McpFileRead.Read(buffer);
            }
        }
        catch (FileNotFoundException)
        {
            return McpFileRead.Missing;
        }
        catch (DirectoryNotFoundException)
        {
            return McpFileRead.Missing;
        }
        catch (IOException)
        {
            return McpFileRead.Unreadable;
        }
        catch (UnauthorizedAccessException)
        {
            return McpFileRead.Unreadable;
        }
    }

    /// <inheritdoc />
    public async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, content, Utf8, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }
}
