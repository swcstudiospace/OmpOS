namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// The real file system for the managed install. A lock is detected by opening the file with no sharing: Windows refuses
/// that while an executable runs. Linux keeps running executables openable, so there a file is never reported locked.
/// </summary>
public sealed class OmpSystemFileSystem : IOmpFileSystem
{
    /// <inheritdoc />
    public void MoveDirectory(string source, string destination) => Directory.Move(source, destination);

    /// <inheritdoc />
    public bool IsFileLocked(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var probe = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
