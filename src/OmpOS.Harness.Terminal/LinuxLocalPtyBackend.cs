namespace OmpOS.Harness.Terminal;

/// <summary>
/// Starts interactive sessions in a Linux pseudo-terminal, over the same <see cref="PtyCore"/> that
/// <see cref="PtyProcessRunner"/> uses. It runs only on Linux: on any other operating system <see cref="StartAsync"/>
/// throws <see cref="PlatformNotSupportedException"/>, because no Windows or macOS backend exists yet.
/// </summary>
/// <remarks>
/// The executable and the working directory must be absolute, and the caller supplies both. No shell is chosen from the
/// environment, and no command string is built: the arguments go to the operating system as a list. The command receives
/// only the host variables PATH, HOME, LANG and TERM; every other host variable is removed from its environment.
/// </remarks>
public sealed class LinuxLocalPtyBackend : ITerminalBackend
{
    /// <summary>
    /// The only host environment variables the command receives. This is the allowlist of OmpOS.Harness.OmpLauncher's
    /// OmpProcessStarter, and the two lists must change together.
    /// </summary>
    private static readonly string[] AllowedEnvironmentVariables = ["PATH", "HOME", "LANG", "TERM"];

    /// <inheritdoc />
    /// <exception cref="PlatformNotSupportedException">The operating system is not Linux.</exception>
    /// <exception cref="ArgumentNullException">The options, the arguments or the output receiver is null.</exception>
    /// <exception cref="ArgumentException">The executable or the working directory is not an absolute path, or an argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A terminal dimension is less than 1.</exception>
    /// <exception cref="FileNotFoundException">The executable does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">The working directory does not exist.</exception>
    public async Task<ITerminalSession> StartAsync(TerminalSessionOptions options, CancellationToken cancellationToken)
    {
        ValidateStart(options);

        var core = await PtyCore.SpawnAsync(
            options.FileName,
            options.Arguments,
            options.WorkingDirectory,
            options.Columns,
            options.Rows,
            ChildEnvironment(),
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
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Columns, 1, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Rows, 1, nameof(options));

        if (options.Arguments.Any(static argument => argument is null))
        {
            throw new ArgumentException(
                "Every argument must be a non-null string.",
                nameof(options));
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
    /// Builds the command's environment. Porta.Pty merges this dictionary into the inherited environment, and an empty value
    /// removes the variable, so every host variable is blanked and the allowlisted ones are then given their host values.
    /// </summary>
    private static Dictionary<string, string> ChildEnvironment()
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

        return environment;
    }
}
