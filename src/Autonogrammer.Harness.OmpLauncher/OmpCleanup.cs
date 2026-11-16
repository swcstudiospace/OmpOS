namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>Best-effort deletion for partial downloads, temporary files and superseded directories.</summary>
internal static class OmpCleanup
{
    /// <summary>Deletes a file if it exists. A file that cannot be deleted now is left for a later launch.</summary>
    /// <param name="path">The file to delete.</param>
    public static void DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A file held by another process stays until a later launch removes it.
        }
        catch (UnauthorizedAccessException)
        {
            // The same rule applies when permissions refuse the delete.
        }
    }

    /// <summary>Deletes a directory tree if it exists. A tree that cannot be deleted now is left for a later launch.</summary>
    /// <param name="path">The directory to delete.</param>
    public static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A directory a running process holds stays until a later launch removes it.
        }
        catch (UnauthorizedAccessException)
        {
            // The same rule applies when permissions refuse the delete.
        }
    }
}
