namespace OmpOS.Harness.Mcp.Tests.Config;

/// <summary>A fresh temporary directory, removed on disposal.</summary>
internal sealed class TempDirectory : IDisposable
{
    /// <summary>Creates the directory.</summary>
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ompos-mcp-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the absolute path of the directory.</summary>
    public string Path { get; }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
