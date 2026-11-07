using System.Text;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// Atomic replacement of a small desktop state file. A write goes to a temporary file in the same directory, is flushed to disk, and
/// then replaces the target by a rename, so a failed write leaves the previous contents in force.
/// </summary>
internal static class DispatchStateFile
{
    /// <summary>Reads a UTF-8 state file, or returns null when it does not exist.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The text, or null.</returns>
    public static string? ReadTextOrNull(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
    }

    /// <summary>Replaces a state file atomically.</summary>
    /// <param name="path">The target file.</param>
    /// <param name="text">The new contents.</param>
    public static void WriteAtomic(string path, string text)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw new ArgumentException("The state path has no directory.", nameof(path));
        _ = Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }

            throw;
        }
    }
}
