namespace Autonogrammer.Harness.OmpLauncher.Tests;

/// <summary>A file seam whose moves can be refused and whose locked file can be named.</summary>
internal sealed class FakeFileSystem : IOmpFileSystem
{
    /// <summary>When true, every move fails with a sharing violation.</summary>
    public bool FailMoves { get; set; }

    /// <summary>The one path reported as locked, or null.</summary>
    public string? LockedPath { get; set; }

    /// <inheritdoc />
    public void MoveDirectory(string source, string destination)
    {
        if (FailMoves)
        {
            throw new IOException("The process cannot access the file because it is being used by another process.");
        }

        Directory.Move(source, destination);
    }

    /// <inheritdoc />
    public bool IsFileLocked(string path) => LockedPath is not null && string.Equals(path, LockedPath, StringComparison.Ordinal);
}
