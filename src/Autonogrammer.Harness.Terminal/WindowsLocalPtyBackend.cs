namespace Autonogrammer.Harness.Terminal;

/// <summary>
/// Starts interactive sessions in a Windows pseudo-terminal, over the same <see cref="PtyCore"/> that
/// <see cref="LinuxLocalPtyBackend"/> uses. It runs only on Windows: on any other operating system
/// <see cref="StartAsync"/> throws <see cref="PlatformNotSupportedException"/> after the request has been checked.
/// </summary>
/// <remarks>
/// The executable and the working directory must be absolute, and the caller supplies both. No shell is chosen from the
/// environment, and no command string is built: the arguments go to the operating system as a list.
/// Porta.Pty merges the dictionary it is given into the environment the process already inherited. This backend
/// therefore passes only <see cref="TerminalSessionOptions.Environment"/>. Every other variable stays, which is the
/// same exposure as launching omp from a shell on that machine, because the desktop app and omp run as the same user.
/// An empty value removes that name. Linux does not do this: <see cref="LinuxLocalPtyBackend"/> keeps an allowlist
/// and unsets every other host variable by name. The difference is recorded as OQ-516.
/// </remarks>
public sealed class WindowsLocalPtyBackend : ITerminalBackend
{
    /// <inheritdoc />
    /// <exception cref="PlatformNotSupportedException">The operating system is not Windows.</exception>
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

        var core = await PtyCore.SpawnAsync(
            options.FileName,
            options.Arguments,
            options.WorkingDirectory,
            options.Columns,
            options.Rows,
            EnvironmentChange(options.Environment),
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
    /// The change set Porta.Pty merges into the inherited environment. Host variables are absent from this dictionary
    /// on purpose, so they are kept. An entry replaces a host variable of the same name, and an empty value removes it.
    /// </summary>
    /// <param name="extra">The caller's variables. They are copied; the caller's dictionary is not kept.</param>
    /// <returns>A new dictionary holding only <paramref name="extra"/>.</returns>
    internal static Dictionary<string, string> EnvironmentChange(IReadOnlyDictionary<string, string> extra)
    {
        return new Dictionary<string, string>(extra, StringComparer.Ordinal);
    }

    /// <summary>
    /// Checks the start request before anything is spawned. The operating-system check is last, so an invalid request
    /// is refused the same way on every host. Each check throws the exception documented on <see cref="StartAsync"/>.
    /// </summary>
    private static void ValidateStart(TerminalSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
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

        if (!Path.IsPathFullyQualified(options.WorkingDirectory))
        {
            throw new ArgumentException("The working directory must be an absolute path.", nameof(options));
        }

        if (!Directory.Exists(options.WorkingDirectory))
        {
            throw new DirectoryNotFoundException($"The working directory '{options.WorkingDirectory}' does not exist.");
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The Windows local terminal backend runs only on Windows.");
        }
    }
}
