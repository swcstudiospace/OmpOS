namespace Autonogrammer.Harness.AgentLoop.Tests.Sessions;

/// <summary>A temporary directory that is deleted when disposed.</summary>
internal sealed class SessionTestDirectory : IDisposable
{
    /// <summary>Creates an empty directory under the system temporary directory.</summary>
    public SessionTestDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "autonogrammer-agentloop-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the directory path.</summary>
    public string Path { get; }

    /// <summary>Gets the path of the registry state file in this directory.</summary>
    public string StatePath => PathOf("sessions.json");

    /// <summary>Returns the path of a child of this directory.</summary>
    /// <param name="name">The child name.</param>
    /// <returns>The full path.</returns>
    public string PathOf(string name) => System.IO.Path.Combine(Path, name);

    /// <summary>Deletes the directory and everything in it.</summary>
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
