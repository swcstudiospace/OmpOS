namespace Autonogrammer.Native.Tests.Settings.Head;

/// <summary>A temporary directory for one test, deleted on disposal.</summary>
internal sealed class HeadTempDirectory : IDisposable
{
    public HeadTempDirectory()
    {
        Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "autonogrammer-head-settings-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Root);
    }

    /// <summary>Gets the root of the directory.</summary>
    public string Root { get; }

    /// <summary>Gets the full path of a path below the root.</summary>
    /// <param name="relativePath">The path below the root, with <c>/</c> separators.</param>
    /// <returns>The full path.</returns>
    public string PathOf(string relativePath) => Path.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Writes a UTF-8 file below the root, creating its directories.</summary>
    /// <param name="relativePath">The path below the root, with <c>/</c> separators.</param>
    /// <param name="content">The text.</param>
    /// <returns>The full path.</returns>
    public string Write(string relativePath, string content)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
