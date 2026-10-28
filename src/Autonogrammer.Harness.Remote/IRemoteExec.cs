namespace Autonogrammer.Harness.Remote;

/// <summary>
/// Runs short POSIX shell scripts inside one environment without a terminal and without a prompt: an ssh host (<see cref="RemoteExec"/>), a podman container
/// (<see cref="Containers.PodmanRemoteExec"/>) or a WSL distribution (<see cref="Containers.WslRemoteExec"/>). It is the transport of the remote file system and of the remote convention
/// discovery, so those two work the same way in every environment. Implementations never use a local shell, never prompt, and never put a secret on a command line.
/// </summary>
public interface IRemoteExec : IDisposable
{
    /// <summary>Runs one script in the environment and waits for it.</summary>
    /// <param name="script">One line of POSIX shell. It is passed to <c>sh -c</c> in the environment; every dynamic value in it must be quoted with <see cref="PosixQuoting"/>.</param>
    /// <param name="input">The bytes written to the script's stdin, then stdin is closed. Empty for none.</param>
    /// <param name="maxOutputBytes">The most stdout bytes kept; the rest is drained and dropped, and the result says so.</param>
    /// <param name="cancellationToken">Cancels the run and stops the client process.</param>
    /// <returns>What the script did. A failing environment is a result, not an exception.</returns>
    Task<RemoteExecResult> RunAsync(string script, ReadOnlyMemory<byte> input, int maxOutputBytes, CancellationToken cancellationToken);

    /// <summary>Resolves the home directory of the user in the environment (<c>printf %s "$HOME"</c>).</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The absolute home directory, or <see langword="null"/> when it could not be read or is not an absolute path.</returns>
    Task<string?> ResolveHomeDirectoryAsync(CancellationToken cancellationToken);

    /// <summary>Gets the absolute POSIX path of the workspace root of the environment: the default root of the remote convention discovery.</summary>
    string WorkspaceRoot { get; }
}
