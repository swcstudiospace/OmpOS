using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Terminal;

namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>
/// Builds and starts the interactive terminals of a WSL environment: a login shell in the workspace folder and the omp command line in it, each <c>wsl.exe</c> run in a
/// local pseudo-terminal through <see cref="ITerminalBackend"/> (Windows only, and only once a Windows terminal backend exists, OQ-69). Nothing secret travels here,
/// because stdin of the terminal is the keyboard: provider tokens of a launch are left out and omp uses its own login in the distribution.
/// </summary>
public sealed class WslTerminalLauncher
{
    private readonly WslCommandBuilder _builder;

    /// <summary>Initializes a new instance of the <see cref="WslTerminalLauncher"/> class.</summary>
    /// <param name="options">How wsl.exe is run, or <see langword="null"/> for the defaults.</param>
    public WslTerminalLauncher(WslClientOptions? options = null)
    {
        _builder = new WslCommandBuilder(options);
    }

    /// <summary>Builds a login shell in the workspace folder of the distribution.</summary>
    /// <param name="profile">The distribution.</param>
    /// <returns>The terminal command.</returns>
    /// <exception cref="ArgumentException">The profile is not valid.</exception>
    public RemoteTerminalCommand BuildShell(WslProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new RemoteTerminalCommand
        {
            Invocation = _builder.BuildTerminal(profile, RemoteTerminalLauncher.ShellScript(profile.WorkspacePath)),
            OmittedEnvironmentNames = [],
        };
    }

    /// <summary>
    /// Builds the omp command line in the workspace folder (<c>exec omp ...</c>) from the same launch an ACP session uses. Secret environment variables and the
    /// settings overlay file are left out (see <see cref="RemoteTerminalCommand"/>).
    /// </summary>
    /// <param name="profile">The distribution.</param>
    /// <param name="launch">The launch from <see cref="WslSessionResources"/>.</param>
    /// <param name="options">What omp starts with, or <see langword="null"/> for a new session without a prompt.</param>
    /// <returns>The terminal command.</returns>
    /// <exception cref="ArgumentException">The profile or the launch is not safe to turn into a shell line, or the resume reference or prompt is not allowed.</exception>
    public RemoteTerminalCommand BuildOmp(WslProfile profile, RemoteLaunch launch, RemoteOmpTerminalOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(launch);
        var (script, omitted, overlayOmitted) = RemoteTerminalLauncher.OmpScript(launch, options);
        return new RemoteTerminalCommand
        {
            Invocation = _builder.BuildTerminal(profile, script),
            OmittedEnvironmentNames = omitted,
            ConfigOverlayOmitted = overlayOmitted,
        };
    }

    /// <summary>Starts a built terminal in a local pseudo-terminal. wsl.exe is found on <c>PATH</c> when its path is not absolute.</summary>
    /// <param name="backend">The terminal backend.</param>
    /// <param name="command">The terminal command from one of the build methods.</param>
    /// <param name="output">Receives the terminal output.</param>
    /// <param name="columns">The starting width, at least 1.</param>
    /// <param name="rows">The starting height, at least 1.</param>
    /// <param name="localWorkingDirectory">An existing local directory wsl.exe starts in, or <see langword="null"/> for the user's home directory.</param>
    /// <param name="cancellationToken">Cancels the spawn.</param>
    /// <returns>The running terminal session.</returns>
    /// <exception cref="FileNotFoundException">wsl.exe was not found.</exception>
    /// <exception cref="ArgumentException">The command carries data for the stdin preamble: a terminal cannot send it.</exception>
    public static Task<ITerminalSession> StartAsync(
        ITerminalBackend backend,
        RemoteTerminalCommand command,
        IProcessOutputSink output,
        int columns,
        int rows,
        string? localWorkingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RemoteTerminalLauncher.StartInvocationAsync(backend, command.Invocation, output, columns, rows, localWorkingDirectory, cancellationToken);
    }
}
