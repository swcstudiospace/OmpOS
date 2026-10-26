namespace OmpOS.Harness.AgentLoop.Tests.Substrate;

/// <summary>A uniquely named temporary directory that is deleted, with everything in it, when disposed.</summary>
internal sealed class SubstrateTempDirectory : IDisposable
{
    /// <summary>Creates the directory.</summary>
    public SubstrateTempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ompos-substrate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the absolute path of the directory.</summary>
    public string Path { get; }

    /// <summary>Writes a file below the directory, creating parent directories.</summary>
    /// <param name="relativePath">The path relative to the directory.</param>
    /// <param name="content">The file text.</param>
    /// <returns>The absolute path of the file.</returns>
    public string Write(string relativePath, string content)
    {
        var full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
