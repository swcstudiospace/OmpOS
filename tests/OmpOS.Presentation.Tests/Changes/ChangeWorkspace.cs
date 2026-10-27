using OmpOS.Presentation.Editor;

namespace OmpOS.Presentation.Tests;

/// <summary>A real workspace folder in a sandbox, with a sibling folder outside it, for the change-review tests.</summary>
internal sealed class ChangeWorkspace : IDisposable
{
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "ompos-changes-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates the workspace root and the folder outside it.</summary>
    public ChangeWorkspace()
    {
        Root = Path.Combine(_sandbox, "root");
        Outside = Path.Combine(_sandbox, "outside");
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Outside);
        FileSystem = new LocalWorkspaceFileSystem(Root);
    }

    /// <summary>Gets the absolute path of the workspace root.</summary>
    public string Root { get; }

    /// <summary>Gets the absolute path of a folder next to the root, outside the workspace.</summary>
    public string Outside { get; }

    /// <summary>Gets the file system over the root.</summary>
    public LocalWorkspaceFileSystem FileSystem { get; }

    /// <summary>Writes a file, and its parent folders, in the workspace.</summary>
    /// <param name="relativePath">The workspace-relative path.</param>
    /// <param name="text">The text.</param>
    public void Write(string relativePath, string text)
    {
        var path = Absolute(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>Reads a file of the workspace.</summary>
    /// <param name="relativePath">The workspace-relative path.</param>
    /// <returns>The text.</returns>
    public string Read(string relativePath) => File.ReadAllText(Absolute(relativePath));

    /// <summary>Tells whether a file exists in the workspace.</summary>
    /// <param name="relativePath">The workspace-relative path.</param>
    /// <returns>True when it does.</returns>
    public bool Exists(string relativePath) => File.Exists(Absolute(relativePath));

    /// <summary>Gets the absolute path of a workspace-relative path.</summary>
    /// <param name="relativePath">The workspace-relative path.</param>
    /// <returns>The absolute path.</returns>
    public string Absolute(string relativePath) => Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Removes the sandbox, including the links it holds.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_sandbox))
        {
            Directory.Delete(_sandbox, recursive: true);
        }
    }
}
