namespace OmpOS.Harness.Herdr.Tests;

/// <summary>A temporary directory that is deleted when disposed.</summary>
internal sealed class TempDirectory : IDisposable
{
    /// <summary>Creates an empty directory under the system temporary directory.</summary>
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ompos-herdr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the directory path.</summary>
    public string Path { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
