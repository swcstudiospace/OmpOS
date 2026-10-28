using OmpOS.Contracts.Environments;
using OmpOS.Harness.Terminal;

namespace OmpOS.Harness.Remote;

/// <summary>
/// Builds and starts the interactive terminals of a remote environment: a login shell in the remote workspace, the omp command line in it, and the
/// host trust terminal. Each is the user's own <c>ssh -tt</c> run in a local pseudo-terminal through <see cref="ITerminalBackend"/> (Linux only until the
/// Windows and macOS backends exist, OQ-69). The terminal is also where ssh asks the user to confirm the key of a host seen for the first time: OmpOS
/// never answers for the user and never edits <c>known_hosts</c>. Nothing secret travels here, because stdin of the terminal is the keyboard: provider
/// tokens of a launch are left out and the remote omp uses its own login.
/// </summary>
public sealed class RemoteTerminalLauncher
{
    private const string LoginShellScript = "exec \"${SHELL:-/bin/sh}\" -l";

    private readonly SshCommandBuilder _builder;

    /// <summary>Initializes a new instance of the <see cref="RemoteTerminalLauncher"/> class.</summary>
    /// <param name="options">How ssh is run, or <see langword="null"/> for the defaults.</param>
    public RemoteTerminalLauncher(SshClientOptions? options = null)
    {
        _builder = new SshCommandBuilder(options ?? new SshClientOptions());
    }

    /// <summary>Builds a login shell in the remote workspace directory.</summary>
    /// <param name="profile">The remote machine.</param>
    /// <returns>The terminal command: <c>ssh -tt alias 'cd root &amp;&amp; exec "${SHELL:-/bin/sh}" -l'</c> with the usual hardening.</returns>
    /// <exception cref="ArgumentException">The profile is not valid.</exception>
    public RemoteTerminalCommand BuildShell(SshProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new RemoteTerminalCommand { Invocation = _builder.BuildTerminal(profile, ShellScript(profile.RemoteWorkspaceRoot)), OmittedEnvironmentNames = [] };
    }

    /// <summary>Builds the host trust terminal: ssh connects, shows the host key fingerprint for the user to confirm, runs <c>true</c> and exits.</summary>
    /// <param name="profile">The remote machine.</param>
    /// <returns>The terminal command.</returns>
    /// <exception cref="ArgumentException">The profile is not valid.</exception>
    public RemoteTerminalCommand BuildHostTrust(SshProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new RemoteTerminalCommand { Invocation = _builder.BuildHostTrustTerminal(profile), OmittedEnvironmentNames = [] };
    }

    /// <summary>
    /// Builds the omp command line in the remote workspace (<c>exec omp ...</c>) from the same launch an ACP session uses, so the terminal runs the same
    /// plugins and directories. Secret environment variables and the settings overlay file are left out (see <see cref="RemoteTerminalCommand"/>).
    /// </summary>
    /// <param name="profile">The remote machine.</param>
    /// <param name="launch">The launch from <see cref="RemoteLaunchMaterializer.Materialize"/>.</param>
    /// <param name="options">What omp starts with, or <see langword="null"/> for a new session without a prompt.</param>
    /// <returns>The terminal command.</returns>
    /// <exception cref="ArgumentException">The profile or the launch is not safe to turn into a shell line, or the resume reference or prompt is not allowed.</exception>
    public RemoteTerminalCommand BuildOmp(SshProfile profile, RemoteLaunch launch, RemoteOmpTerminalOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(launch);
        var (script, omitted, overlayOmitted) = OmpScript(launch, options);
        var invocation = _builder.BuildTerminal(profile, script);
        return new RemoteTerminalCommand { Invocation = invocation, OmittedEnvironmentNames = omitted, ConfigOverlayOmitted = overlayOmitted };
    }

    /// <summary>The script of a login shell in a workspace folder: <c>PATH</c> setup, <c>cd</c> (exit code 71 when the folder is missing) and an exec of the login shell.</summary>
    /// <param name="workspaceRoot">The absolute workspace folder in the environment.</param>
    /// <returns>The one-line script.</returns>
    internal static string ShellScript(string workspaceRoot) =>
        SshCommandBuilder.PathSetup + "; cd " + PosixQuoting.Quote(workspaceRoot) + " || exit 71; " + LoginShellScript;

    /// <summary>
    /// The script of the interactive omp of a launch: <c>PATH</c> setup, the non-secret environment, <c>cd</c> and <c>exec omp ...</c>. Secret variables and the
    /// settings overlay are left out because a terminal has no private channel; what was left out is returned.
    /// </summary>
    /// <param name="launch">The launch.</param>
    /// <param name="options">What omp starts with, or <see langword="null"/>.</param>
    /// <returns>The one-line script, the names of the environment variables that were left out, and whether the overlay was left out.</returns>
    /// <exception cref="ArgumentException">The launch is not safe to turn into a shell line, or the resume reference or prompt is not allowed.</exception>
    internal static (string Script, List<string> OmittedEnvironmentNames, bool ConfigOverlayOmitted) OmpScript(RemoteLaunch launch, RemoteOmpTerminalOptions? options)
    {
        if (!SshProfileRules.IsValidAbsoluteRemotePath(launch.WorkingDirectory) || !SshProfileRules.IsValidRemoteExecutable(launch.OmpPath))
        {
            throw new ArgumentException("The remote working directory and omp path must be valid remote paths.", nameof(launch));
        }

        var omitted = new List<string>();
        var statements = new List<string> { SshCommandBuilder.PathSetup };
        statements.AddRange(ExportStatements(launch, omitted));
        statements.Add("cd " + PosixQuoting.Quote(launch.WorkingDirectory) + " || exit 71");
        var overlayOmitted = FilterArguments(launch, out var arguments);
        statements.Add(ExecStatement(launch.OmpPath, arguments, options));
        return (string.Join("; ", statements), omitted, overlayOmitted);
    }

    /// <summary>
    /// Starts a built terminal in a local pseudo-terminal. The ssh client is found on <c>PATH</c> when its path is not absolute (the terminal backend takes an
    /// absolute executable only).
    /// </summary>
    /// <param name="backend">The terminal backend.</param>
    /// <param name="command">The terminal command from one of the build methods.</param>
    /// <param name="output">Receives the terminal output.</param>
    /// <param name="columns">The starting width, at least 1.</param>
    /// <param name="rows">The starting height, at least 1.</param>
    /// <param name="localWorkingDirectory">An existing local directory ssh starts in (it only matters for relative identity files), or <see langword="null"/> for the user's home directory.</param>
    /// <param name="cancellationToken">Cancels the spawn.</param>
    /// <returns>The running terminal session.</returns>
    /// <exception cref="FileNotFoundException">The ssh client was not found.</exception>
    /// <exception cref="ArgumentException">The command carries data for the stdin preamble: a terminal cannot send it.</exception>
    public static async Task<ITerminalSession> StartAsync(
        ITerminalBackend backend,
        RemoteTerminalCommand command,
        IProcessOutputSink output,
        int columns,
        int rows,
        string? localWorkingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await StartInvocationAsync(backend, command.Invocation, output, columns, rows, localWorkingDirectory, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts any client invocation (ssh, <c>podman exec -it</c>, <c>wsl.exe</c>) in a local pseudo-terminal. A client whose path is not absolute is found on <c>PATH</c>.
    /// </summary>
    /// <param name="backend">The terminal backend.</param>
    /// <param name="invocation">The client process. It carries no stdin preamble.</param>
    /// <param name="output">Receives the terminal output.</param>
    /// <param name="columns">The starting width, at least 1.</param>
    /// <param name="rows">The starting height, at least 1.</param>
    /// <param name="localWorkingDirectory">An existing local directory the client starts in, or <see langword="null"/> for the user's home directory.</param>
    /// <param name="cancellationToken">Cancels the spawn.</param>
    /// <returns>The running terminal session.</returns>
    /// <exception cref="FileNotFoundException">The client was not found.</exception>
    /// <exception cref="ArgumentException">The invocation carries data for the stdin preamble: a terminal cannot send it.</exception>
    internal static async Task<ITerminalSession> StartInvocationAsync(
        ITerminalBackend backend,
        SshInvocation invocation,
        IProcessOutputSink output,
        int columns,
        int rows,
        string? localWorkingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(output);
        if (invocation.Preamble is not null)
        {
            throw new ArgumentException("A terminal has no private channel: the command must not carry a stdin preamble.", nameof(invocation));
        }

        var executable = ResolveExecutable(invocation.FileName);
        var directory = localWorkingDirectory ?? LocalHome();
        var terminal = new TerminalSessionOptions(executable, invocation.Arguments, directory, columns, rows, output);
        return await backend.StartAsync(terminal, cancellationToken).ConfigureAwait(false);
    }

    private static string LocalHome()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(home) || !Directory.Exists(home) ? Path.GetTempPath() : home;
    }

    private static string ResolveExecutable(string fileName)
    {
        if (Path.IsPathFullyQualified(fileName))
        {
            return fileName;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("The client '" + fileName + "' was not found on PATH.", fileName);
    }

    private static IEnumerable<string> ExportStatements(RemoteLaunch launch, List<string> omitted)
    {
        foreach (var name in launch.Environment.Keys.Order(StringComparer.Ordinal))
        {
            var value = launch.Environment[name];
            var allowed = !launch.SecretEnvironmentNames.Contains(name)
                && SshCommandBuilder.IsAllowedEnvironmentName(name)
                && !value.Contains('\n', StringComparison.Ordinal)
                && !value.Contains('\r', StringComparison.Ordinal)
                && !value.Contains('\0', StringComparison.Ordinal);
            if (allowed)
            {
                yield return "export " + name + "=" + PosixQuoting.Quote(value);
            }
            else
            {
                omitted.Add(name);
            }
        }
    }

    // Drops the "--config <overlay>" pair when the overlay is a file of the launch: the file cannot be written without the private channel.
    private static bool FilterArguments(RemoteLaunch launch, out List<string> arguments)
    {
        arguments = [];
        var overlayPaths = launch.Files.Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
        var omitted = false;
        for (var index = 0; index < launch.Arguments.Count; index++)
        {
            var argument = launch.Arguments[index];
            var isOverlayPair = string.Equals(argument, "--config", StringComparison.Ordinal)
                && index + 1 < launch.Arguments.Count
                && overlayPaths.Contains(launch.Arguments[index + 1]);
            if (isOverlayPair)
            {
                omitted = true;
                index++;
            }
            else
            {
                arguments.Add(argument);
            }
        }

        return omitted;
    }

    private static string ExecStatement(string ompPath, List<string> arguments, RemoteOmpTerminalOptions? options)
    {
        var words = new List<string> { "exec", PosixQuoting.Quote(ompPath) };
        words.AddRange(arguments.Select(PosixQuoting.Quote));
        if (options?.NativeSessionId is { } resume)
        {
            if (string.IsNullOrWhiteSpace(resume) || resume[0] == '-' || resume.Contains('\0', StringComparison.Ordinal))
            {
                throw new ArgumentException("The resume reference must be a non-blank value that does not start with '-'.", nameof(options));
            }

            words.Add(PosixQuoting.Quote("--resume"));
            words.Add(PosixQuoting.Quote(resume));
        }

        if (options?.InitialPrompt is { } prompt && !string.IsNullOrWhiteSpace(prompt))
        {
            if (prompt[0] is '-' or '@' || prompt.Contains('\0', StringComparison.Ordinal))
            {
                throw new ArgumentException("The first prompt must not start with '-' or '@'.", nameof(options));
            }

            words.Add(PosixQuoting.Quote(prompt));
        }

        return string.Join(' ', words);
    }
}
