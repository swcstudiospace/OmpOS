namespace OmpOS.Harness.Terminal;

/// <summary>
/// Starts interactive sessions in a Linux pseudo-terminal, over the same <see cref="PtyCore"/> that
/// <see cref="PtyProcessRunner"/> uses. It runs only on Linux: on any other operating system <see cref="StartAsync"/>
/// throws <see cref="PlatformNotSupportedException"/>, because no Windows or macOS backend exists yet.
/// </summary>
/// <remarks>
/// The executable and the working directory must be absolute, and the caller supplies both. No shell is chosen from the
/// environment, and no command string is built: the arguments go to the operating system as a list. The command receives
/// only the host variables PATH, HOME, LANG, TERM, PI_CODING_AGENT_DIR and OMP_PROFILE, plus the variables the caller
/// passes explicitly in <see cref="TerminalSessionOptions.Environment"/>; every other host variable is removed from its environment.
/// The pseudo-terminal helper adds the variables it is given to the environment it inherits instead of replacing it, so the command is
/// started through <c>/usr/bin/env -u NAME ...</c>, which unsets each host variable by name. Only names reach the command line, never values.
/// </remarks>
public sealed class LinuxLocalPtyBackend : ITerminalBackend
{
    /// <summary>
    /// The only host environment variables the command receives. The first four are the allowlist of OmpOS.Harness.OmpLauncher's
    /// OmpProcessStarter. PI_CODING_AGENT_DIR and OMP_PROFILE are added because an interactive omp takes its session store and
    /// its profile from them; they are passed only when the host has them.
    /// </summary>
    private static readonly string[] AllowedEnvironmentVariables =
        ["PATH", "HOME", "LANG", "TERM", "PI_CODING_AGENT_DIR", "OMP_PROFILE"];

    /// <summary>The program that unsets the inherited host variables by name and then runs the command.</summary>
    private const string EnvProgram = "/usr/bin/env";

    /// <inheritdoc />
    /// <exception cref="PlatformNotSupportedException">The operating system is not Linux.</exception>
    /// <exception cref="ArgumentNullException">The options, the arguments or the output receiver is null.</exception>
    /// <exception cref="ArgumentException">
    /// The executable or the working directory is not an absolute path, an argument is null, or an environment variable name or
    /// value is invalid. The message never contains an environment value.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A terminal dimension is less than 1.</exception>
    /// <exception cref="FileNotFoundException">The executable does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">The working directory does not exist.</exception>
    public async Task<ITerminalSession> StartAsync(TerminalSessionOptions options, CancellationToken cancellationToken)
    {
        ValidateStart(options);

        var childEnvironment = ChildEnvironment(options.Environment);
        var core = await PtyCore.SpawnAsync(
            EnvProgram,
            UnsetThenRun(childEnvironment, options.FileName, options.Arguments),
            options.WorkingDirectory,
            options.Columns,
            options.Rows,
            childEnvironment,
            cancellationToken).ConfigureAwait(false);

        try
        {
            return new PtyTerminalSession(core, options.Output);
        }
        catch (Exception)
        {
            // The session never took ownership of the terminal, so this method must release it before the failure propagates.
            core.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Checks the start request before anything is spawned. Each check throws the exception documented on
    /// <see cref="StartAsync"/>; the parameter name is always <c>options</c>, the parameter that holds the value checked.
    /// </summary>
    private static void ValidateStart(TerminalSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "The local terminal backend runs only on Linux. Windows and macOS have no local terminal backend yet.");
        }

        ArgumentNullException.ThrowIfNull(options.Arguments, nameof(options));
        ArgumentNullException.ThrowIfNull(options.Output, nameof(options));
        ArgumentNullException.ThrowIfNull(options.Environment, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Columns, 1, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Rows, 1, nameof(options));

        if (options.Arguments.Any(static argument => argument is null))
        {
            throw new ArgumentException(
                "Every argument must be a non-null string.",
                nameof(options));
        }

        var environmentProblem = TerminalSessionOptions.FindEnvironmentProblem(options.Environment);
        if (environmentProblem is not null)
        {
            throw new ArgumentException(environmentProblem, nameof(options));
        }

        if (!Path.IsPathFullyQualified(options.FileName))
        {
            throw new ArgumentException(
                "The executable must be an absolute path. It is never searched for on the path.",
                nameof(options));
        }

        if (!File.Exists(options.FileName))
        {
            throw new FileNotFoundException("The executable does not exist.", options.FileName);
        }

        if (!File.Exists(EnvProgram))
        {
            throw new FileNotFoundException("The program that clears the host environment does not exist, so no command is started with the host's variables.", EnvProgram);
        }

        if (!Path.IsPathFullyQualified(options.WorkingDirectory))
        {
            throw new ArgumentException("The working directory must be an absolute path.", nameof(options));
        }

        if (!Directory.Exists(options.WorkingDirectory))
        {
            throw new DirectoryNotFoundException($"The working directory '{options.WorkingDirectory}' does not exist.");
        }
    }

    /// <summary>
    /// Builds the command line that unsets every blanked host variable by name, then runs the command. A blanked variable is one the
    /// environment gives an empty value, so it is neither allowlisted nor passed explicitly. No value is ever put on the command line.
    /// </summary>
    private static string[] UnsetThenRun(Dictionary<string, string> environment, string fileName, IReadOnlyList<string> arguments)
    {
        var line = new List<string>(environment.Count * 2 + arguments.Count + 1);
        foreach (var (name, value) in environment)
        {
            if (value.Length == 0)
            {
                line.Add("-u");
                line.Add(name);
            }
        }

        line.Add(fileName);
        line.AddRange(arguments);
        return [.. line];
    }

    /// <summary>
    /// Builds the command's environment. Porta.Pty merges this dictionary into the inherited environment, and an empty value
    /// removes the variable, so every host variable is blanked, the allowlisted ones are then given their host values, and the
    /// caller's variables are applied last so that they win.
    /// </summary>
    private static Dictionary<string, string> ChildEnvironment(IReadOnlyDictionary<string, string> extra)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string name in Environment.GetEnvironmentVariables().Keys)
        {
            environment[name] = string.Empty;
        }

        foreach (var name in AllowedEnvironmentVariables)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (value is not null)
            {
                environment[name] = value;
            }
        }

        foreach (var (name, value) in extra)
        {
            environment[name] = value;
        }

        return environment;
    }
}
