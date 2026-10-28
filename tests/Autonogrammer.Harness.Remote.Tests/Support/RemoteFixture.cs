namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>
/// A fake remote machine with its ssh exec layer and a directory outside the workspace that a symbolic link can point to. The "remote" files are
/// ordinary files in a temporary directory, so a test builds a tree with <see cref="File"/> calls and reads it back through ssh.
/// </summary>
internal sealed class RemoteFixture : IDisposable
{
    private RemoteFixture(FakeRemoteHost host, RemoteExec exec, TempDirectory outside)
    {
        Host = host;
        Exec = exec;
        Outside = outside;
    }

    /// <summary>Gets the fake machine.</summary>
    public FakeRemoteHost Host { get; }

    /// <summary>Gets the exec layer over the fake machine.</summary>
    public RemoteExec Exec { get; }

    /// <summary>Gets a directory outside the remote workspace.</summary>
    public TempDirectory Outside { get; }

    /// <summary>Gets the remote workspace directory.</summary>
    public string Workspace => Host.WorkspaceDirectory;

    /// <summary>Creates the fixture.</summary>
    /// <param name="options">How the fake machine behaves, or <see langword="null"/> for a healthy one.</param>
    /// <returns>The fixture. Dispose it to delete everything.</returns>
    public static RemoteFixture Create(FakeRemoteHostOptions? options = null)
    {
        var host = FakeRemoteHost.Create(options);
        var exec = new RemoteExec(host.Profile, host.ClientOptions);
        return new RemoteFixture(host, exec, TempDirectory.Create());
    }

    /// <summary>Creates a file under the remote workspace, with its parent directories.</summary>
    /// <param name="relative">The path relative to the workspace, with <c>/</c> separators.</param>
    /// <param name="content">The text.</param>
    /// <returns>The absolute path.</returns>
    public string Write(string relative, string content)
    {
        var path = Path.Combine(Workspace, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Creates a file under the remote home directory, with its parent directories.</summary>
    /// <param name="relative">The path relative to the home directory.</param>
    /// <param name="content">The text.</param>
    /// <returns>The absolute path.</returns>
    public string WriteHome(string relative, string content)
    {
        var path = Path.Combine(Host.HomeDirectory, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Creates the file system over the remote workspace.</summary>
    /// <returns>The file system.</returns>
    public RemoteWorkspaceFileSystem FileSystem() => new(Exec);

    /// <inheritdoc />
    public void Dispose()
    {
        Exec.Dispose();
        Outside.Dispose();
        Host.Dispose();
    }
}
