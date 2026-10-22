namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// A temporary directory that is deleted when disposed. It holds the plugin state file a real consent test uses.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    /// <summary>
    /// Creates an empty directory under the system temporary directory.
    /// </summary>
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ompos-automations-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// The directory path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The path of the plugin state file in this directory.
    /// </summary>
    public string StatePath => System.IO.Path.Combine(Path, "state.json");

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
