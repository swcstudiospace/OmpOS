namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// The file operations the managed install cannot do through plain <c>System.IO</c> calls in tests: moving a staged directory
/// into place and reporting whether a file is held open by another process.
/// </summary>
public interface IOmpFileSystem
{
    /// <summary>Moves a directory to a path that does not exist yet.</summary>
    /// <param name="source">The directory to move.</param>
    /// <param name="destination">The new path.</param>
    /// <exception cref="IOException">The move was refused, for example because a file in use blocks it.</exception>
    /// <exception cref="UnauthorizedAccessException">The move was refused by permissions.</exception>
    void MoveDirectory(string source, string destination);

    /// <summary>Reports whether a file is held open exclusively, as a running Windows executable is.</summary>
    /// <param name="path">The file to check. A missing file is not locked.</param>
    /// <returns>True when the file is locked.</returns>
    bool IsFileLocked(string path);
}
