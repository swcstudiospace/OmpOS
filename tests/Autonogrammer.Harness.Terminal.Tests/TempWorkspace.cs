namespace Autonogrammer.Harness.Terminal.Tests;

/// <summary>
/// A temporary workspace root that is deleted when disposed.
/// </summary>
internal sealed class TempWorkspace : IDisposable
{
    /// <summary>
    /// Creates an empty workspace directory under the system temporary directory.
    /// </summary>
    public TempWorkspace()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "autonogrammer-terminal-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// The workspace root as created, before symbolic links are resolved.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Creates a subdirectory of the workspace and returns its path.
    /// </summary>
    /// <param name="relative">The subdirectory, relative to the workspace root.</param>
    public string CreateSubdirectory(string relative)
    {
        return Directory.CreateDirectory(System.IO.Path.Combine(Path, relative)).FullName;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
