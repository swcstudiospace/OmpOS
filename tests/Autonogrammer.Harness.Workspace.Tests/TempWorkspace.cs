namespace Autonogrammer.Workspace.Tests;

/// <summary>
/// A workspace root in a fresh temporary directory, removed on disposal.
/// </summary>
internal sealed class TempWorkspace : IDisposable
{
    /// <summary>
    /// Creates an empty workspace directory.
    /// </summary>
    public TempWorkspace()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "autonogrammer-workspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
        Root = new WorkspaceRoot(RootPath);
    }

    /// <summary>Gets the absolute path of the workspace directory as created.</summary>
    public string RootPath { get; }

    /// <summary>Gets the workspace root.</summary>
    public WorkspaceRoot Root { get; }

    /// <summary>
    /// Writes a UTF-8 text file, creating its folders.
    /// </summary>
    /// <param name="relativePath">The path relative to the workspace root.</param>
    /// <param name="content">The file content.</param>
    public void Write(string relativePath, string content)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    /// <summary>
    /// Writes raw bytes to a file, creating its folders.
    /// </summary>
    /// <param name="relativePath">The path relative to the workspace root.</param>
    /// <param name="bytes">The file content.</param>
    public void WriteBytes(string relativePath, byte[] bytes)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, bytes);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
