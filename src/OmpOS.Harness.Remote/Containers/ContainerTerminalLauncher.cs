using OmpOS.Contracts.Environments;
using OmpOS.Harness.Terminal;

namespace OmpOS.Harness.Remote.Containers;

/// <summary>
/// Builds and starts the interactive terminals of a container environment: a login shell in the workspace folder and the omp command line in it, each the user's own
/// <c>podman exec -it</c> run in a local pseudo-terminal through <see cref="ITerminalBackend"/>. Nothing secret travels here, because stdin of the terminal is the
/// keyboard: provider tokens of a launch are left out and omp uses its own login inside the container.
/// </summary>
public sealed class ContainerTerminalLauncher
{
    private readonly PodmanExecCommandBuilder _builder;

    /// <summary>Initializes a new instance of the <see cref="ContainerTerminalLauncher"/> class.</summary>
    /// <param name="options">How podman is run, or <see langword="null"/> for the defaults.</param>
    public ContainerTerminalLauncher(PodmanClientOptions? options = null)
    {
        _builder = new PodmanExecCommandBuilder(options);
    }

    /// <summary>Builds a login shell in the workspace folder of the container (<c>podman exec -it</c>).</summary>
    /// <param name="profile">The container.</param>
    /// <returns>The terminal command.</returns>
    /// <exception cref="ArgumentException">The profile is not valid.</exception>
    public RemoteTerminalCommand BuildShell(ContainerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new RemoteTerminalCommand
        {
            Invocation = _builder.BuildTerminal(profile, RemoteTerminalLauncher.ShellScript(profile.WorkspaceFolder)),
            OmittedEnvironmentNames = [],
        };
    }

    /// <summary>
    /// Builds the omp command line in the workspace folder (<c>exec omp ...</c>) from the same launch an ACP session uses. Secret environment variables and the
    /// settings overlay file are left out (see <see cref="RemoteTerminalCommand"/>).
    /// </summary>
    /// <param name="profile">The container.</param>
    /// <param name="launch">The launch from <see cref="ContainerSessionResources"/>.</param>
    /// <param name="options">What omp starts with, or <see langword="null"/> for a new session without a prompt.</param>
    /// <returns>The terminal command.</returns>
    /// <exception cref="ArgumentException">The profile or the launch is not safe to turn into a shell line, or the resume reference or prompt is not allowed.</exception>
    public RemoteTerminalCommand BuildOmp(ContainerProfile profile, RemoteLaunch launch, RemoteOmpTerminalOptions? options = null)
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

    /// <summary>Starts a built terminal in a local pseudo-terminal. The podman client is found on <c>PATH</c> when its path is not absolute.</summary>
    /// <param name="backend">The terminal backend.</param>
    /// <param name="command">The terminal command from one of the build methods.</param>
    /// <param name="output">Receives the terminal output.</param>
    /// <param name="columns">The starting width, at least 1.</param>
    /// <param name="rows">The starting height, at least 1.</param>
    /// <param name="localWorkingDirectory">An existing local directory podman starts in, or <see langword="null"/> for the user's home directory.</param>
    /// <param name="cancellationToken">Cancels the spawn.</param>
    /// <returns>The running terminal session.</returns>
    /// <exception cref="FileNotFoundException">The podman client was not found.</exception>
    /// <exception cref="ArgumentException">The command carries data for the stdin preamble: a terminal cannot send it.</exception>
    public Task<ITerminalSession> StartAsync(
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
