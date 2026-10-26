using OmpOS.Harness.Terminal;

namespace OmpOS.Harness.OmpLauncher;

/// <summary>
/// Opens omp in an interactive terminal. It locates omp with <see cref="OmpLocator"/>, reads the version with
/// <see cref="OmpVersionProbe"/>, then starts the located executable through an <see cref="ITerminalBackend"/> with the
/// argument array and the project directory. A missing omp is returned as <see cref="OmpTerminalNotFound"/>; it never throws.
/// </summary>
public sealed class OmpTerminalLauncher
{
    private readonly ITerminalBackend _backend;
    private readonly OmpLocatorOptions _locatorOptions;

    /// <summary>Creates a launcher.</summary>
    /// <param name="backend">Starts the terminal session.</param>
    /// <param name="locatorOptions">The configured path, the search directories and the known install locations.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public OmpTerminalLauncher(ITerminalBackend backend, OmpLocatorOptions locatorOptions)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(locatorOptions);
        _backend = backend;
        _locatorOptions = locatorOptions;
    }

    /// <summary>
    /// Locates omp and starts it in a terminal session in the project directory.
    /// </summary>
    /// <param name="request">The project directory, the omp arguments, the terminal size and the output receiver.</param>
    /// <param name="cancellationToken">Cancels the version probe and the spawn. A cancelled launch throws.</param>
    /// <returns>
    /// <see cref="OmpTerminalStarted"/> with the running session, <see cref="OmpTerminalNotFound"/> when omp is not
    /// installed, or <see cref="OmpTerminalLaunchFailed"/> when the session could not start.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException">The configured omp path is set but is not absolute. That is a configuration error.</exception>
    /// <exception cref="OperationCanceledException">The launch was cancelled.</exception>
    public async Task<OmpTerminalLaunchResult> LaunchAsync(
        OmpTerminalLaunchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var location = OmpLocator.Locate(_locatorOptions);
        if (location is OmpNotFound notFound)
        {
            return new OmpTerminalNotFound(notFound.TriedPaths, version: null, notFound.InstallGuidance);
        }

        var found = (OmpFound)location;

        if (!Path.IsPathRooted(request.ProjectDirectory))
        {
            return new OmpTerminalLaunchFailed("The project directory must be absolute.");
        }

        if (!Directory.Exists(request.ProjectDirectory))
        {
            return new OmpTerminalLaunchFailed($"The project directory does not exist: {request.ProjectDirectory}");
        }

        var probe = await OmpVersionProbe.ProbeAsync(found.ExecutablePath, cancellationToken).ConfigureAwait(false);
        var version = probe is OmpVersionReported reported ? reported.Version : null;

        var options = new TerminalSessionOptions(
            found.ExecutablePath,
            request.Arguments,
            request.ProjectDirectory,
            request.Columns,
            request.Rows,
            request.Output);

        try
        {
            var session = await _backend.StartAsync(options, cancellationToken).ConfigureAwait(false);
            return new OmpTerminalStarted(found.ExecutablePath, version, session);
        }
        catch (Exception exception) when (exception is IOException or PlatformNotSupportedException)
        {
            return new OmpTerminalLaunchFailed($"The omp terminal could not be started: {exception.Message}");
        }
    }
}
