namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>A temporary directory that is deleted when the test ends.</summary>
internal sealed class TempDirectory : IDisposable
{
    private TempDirectory(string path) => Path = path;

    /// <summary>Gets the absolute path of the directory.</summary>
    public string Path { get; }

    /// <summary>Creates a new empty directory under the system temporary directory.</summary>
    /// <returns>The directory.</returns>
    public static TempDirectory Create()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "autonogrammer-remote-test-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(path);
        return new TempDirectory(path);
    }

    /// <summary>Combines the directory with relative segments.</summary>
    /// <param name="segments">The segments.</param>
    /// <returns>The combined path.</returns>
    public string Combine(params string[] segments) => System.IO.Path.Combine([Path, .. segments]);

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temporary directory does not fail a test.
        }
    }
}
