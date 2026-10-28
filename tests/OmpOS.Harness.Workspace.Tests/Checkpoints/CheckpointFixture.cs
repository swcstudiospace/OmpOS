using System.Text;

namespace OmpOS.Workspace.Tests.Checkpoints;

/// <summary>A workspace folder and a state folder in fresh temporary directories, removed on disposal.</summary>
internal sealed class CheckpointFixture : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "ompos-checkpoints-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates the two folders.</summary>
    public CheckpointFixture()
    {
        Directory.CreateDirectory(WorkspacePath);
        Directory.CreateDirectory(StatePath);
    }

    /// <summary>Gets the workspace folder.</summary>
    public string WorkspacePath => Path.Combine(_base, "workspace");

    /// <summary>Gets the state folder.</summary>
    public string StatePath => Path.Combine(_base, "state");

    /// <summary>Gets the base folder that holds both.</summary>
    public string BasePath => _base;

    /// <summary>Writes a UTF-8 file without a byte order mark.</summary>
    /// <param name="relative">The path inside the workspace.</param>
    /// <param name="text">The content.</param>
    public void Write(string relative, string text) => WriteBytes(relative, new UTF8Encoding(false).GetBytes(text));

    /// <summary>Writes a file.</summary>
    /// <param name="relative">The path inside the workspace.</param>
    /// <param name="bytes">The content.</param>
    public void WriteBytes(string relative, byte[] bytes)
    {
        var path = Path.Combine(WorkspacePath, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>Reads a file as text.</summary>
    /// <param name="relative">The path inside the workspace.</param>
    /// <returns>The text.</returns>
    public string Read(string relative) => File.ReadAllText(Path.Combine(WorkspacePath, relative));

    /// <summary>Reads a file as bytes.</summary>
    /// <param name="relative">The path inside the workspace.</param>
    /// <returns>The bytes.</returns>
    public byte[] ReadBytes(string relative) => File.ReadAllBytes(Path.Combine(WorkspacePath, relative));

    /// <summary>Tells whether a file exists.</summary>
    /// <param name="relative">The path inside the workspace.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public bool Exists(string relative) => File.Exists(Path.Combine(WorkspacePath, relative));

    /// <summary>Removes both folders.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_base))
        {
            Directory.Delete(_base, recursive: true);
        }
    }
}
