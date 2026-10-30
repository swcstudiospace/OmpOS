namespace Autonogrammer.Api.IntegrationTests.WorkspaceSettings;

/// <summary>A temporary workspace folder with helpers to write files and links. Every folder it made is deleted on dispose.</summary>
internal sealed class TempWorkspace : IDisposable
{
    private readonly List<string> _folders = [];

    /// <summary>Initializes a new instance of the <see cref="TempWorkspace"/> class and creates its folder.</summary>
    public TempWorkspace()
    {
        Root = NewFolder();
    }

    /// <summary>Gets the absolute path of the workspace folder.</summary>
    public string Root { get; }

    /// <summary>Creates a second empty temporary folder, for example one outside the workspace.</summary>
    /// <returns>The absolute path.</returns>
    public string NewFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), "autonogrammer-ws-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(path);
        _folders.Add(path);
        return path;
    }

    /// <summary>Writes a UTF-8 text file below a folder, creating the folders on the way.</summary>
    /// <param name="relative">The path below the folder, with <c>/</c> separators.</param>
    /// <param name="content">The text.</param>
    /// <param name="folder">The folder, or null for the workspace folder.</param>
    /// <returns>The absolute path of the file.</returns>
    public string Write(string relative, string content, string? folder = null) => WriteBytes(relative, System.Text.Encoding.UTF8.GetBytes(content), folder);

    /// <summary>Writes a file of raw bytes below a folder, creating the folders on the way.</summary>
    /// <param name="relative">The path below the folder, with <c>/</c> separators.</param>
    /// <param name="bytes">The bytes.</param>
    /// <param name="folder">The folder, or null for the workspace folder.</param>
    /// <returns>The absolute path of the file.</returns>
    public string WriteBytes(string relative, byte[] bytes, string? folder = null)
    {
        var path = Path.Combine(folder ?? Root, relative.Replace('/', Path.DirectorySeparatorChar));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Creates a symbolic link in the workspace folder.</summary>
    /// <param name="relative">The link path below the workspace folder.</param>
    /// <param name="target">The absolute path the link points to; a folder or a file that exists.</param>
    /// <returns>False when the platform or the account cannot create symbolic links.</returns>
    public bool TryLink(string relative, string target)
    {
        var path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            _ = Directory.Exists(target) ? Directory.CreateSymbolicLink(path, target) : File.CreateSymbolicLink(path, target);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var folder in _folders)
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A temporary folder that stays behind is harmless.
            }
        }
    }
}
