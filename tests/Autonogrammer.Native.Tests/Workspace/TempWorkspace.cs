namespace Autonogrammer.Native.Tests.Workspace;

/// <summary>A workspace folder under the system temp directory. The folder and its files are removed on dispose.</summary>
internal sealed class TempWorkspace : IDisposable
{
    /// <summary>Creates an empty workspace folder.</summary>
    public TempWorkspace()
    {
        Root = Directory.CreateTempSubdirectory("autonogrammer-ws-").FullName;
    }

    /// <summary>Gets the absolute path of the workspace folder.</summary>
    public string Root { get; }

    /// <summary>Writes a UTF-8 file at a '/'-separated path under the root, creating its folders.</summary>
    /// <param name="relativePath">The file path relative to the root.</param>
    /// <param name="text">The file content.</param>
    public void Write(string relativePath, string text)
    {
        var fullPath = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, text);
    }

    /// <summary>Removes the workspace folder. Dispose any catalog or watcher over it first.</summary>
    public void Dispose() => Directory.Delete(Root, recursive: true);
}
