namespace OmpOS.Harness.Plugins.Tests;

/// <summary>
/// A temporary directory that is deleted when disposed. It holds the state file and any sentinel a test checks.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    /// <summary>
    /// Creates an empty directory under the system temporary directory.
    /// </summary>
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ompos-plugins-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// The directory path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The path of the plugin state file in this directory.
    /// </summary>
    public string StatePath => PathOf("state.json");

    /// <summary>
    /// Returns the path of a file in this directory.
    /// </summary>
    /// <param name="name">The file name.</param>
    public string PathOf(string name) => System.IO.Path.Combine(Path, name);

    /// <summary>
    /// Deletes the directory and everything in it.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
