namespace OmpOS.Harness.Registry.Tests;

/// <summary>
/// An empty directory under the system temporary directory, removed on disposal.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    /// <summary>
    /// Creates an empty directory.
    /// </summary>
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ompos-registry-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// The directory path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The export file path in this directory.
    /// </summary>
    public string ExportPath => System.IO.Path.Combine(Path, "registry.json");

    /// <summary>
    /// Removes the directory and its contents.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
