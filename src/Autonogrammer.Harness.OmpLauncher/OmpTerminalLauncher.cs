using Autonogrammer.Harness.Terminal;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// Opens omp in an interactive terminal. It locates omp, reads the version with <see cref="OmpVersionProbe"/>, then starts the located
/// executable through an <see cref="ITerminalBackend"/> with the argument array, the project directory and the environment variables of the
/// request. A missing omp is returned as <see cref="OmpTerminalNotFound"/>; it never throws.
/// </summary>
public sealed class OmpTerminalLauncher
{
    private readonly ITerminalBackend _backend;
    private readonly Func<CancellationToken, Task<OmpLocation>> _locate;

    /// <summary>Creates a launcher that locates omp with <see cref="OmpLocator"/> on each launch.</summary>
    /// <param name="backend">Starts the terminal session.</param>
    /// <param name="locatorOptions">The configured path, the search directories and the known install locations.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public OmpTerminalLauncher(ITerminalBackend backend, OmpLocatorOptions locatorOptions)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(locatorOptions);
        _backend = backend;
        _locate = _ => Task.FromResult(OmpLocator.Locate(locatorOptions));
    }

    /// <summary>
    /// Creates a launcher that asks <paramref name="resolveExecutable"/> for the omp each launch runs, so a launch runs what the caller resolves at
    /// that moment. A null answer is a missing omp.
    /// </summary>
    /// <param name="backend">Starts the terminal session.</param>
    /// <param name="resolveExecutable">Returns the absolute executable for a launch, or null when none is runnable.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public OmpTerminalLauncher(ITerminalBackend backend, Func<CancellationToken, Task<string?>> resolveExecutable)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(resolveExecutable);
        _backend = backend;
        _locate = async cancellationToken => Located(await resolveExecutable(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Locates omp and starts it in a terminal session in the project directory.
    /// </summary>
    /// <param name="request">The project directory, the omp arguments, the environment, the terminal size and the output receiver.</param>
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

        var location = await _locate(cancellationToken).ConfigureAwait(false);
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

        var environmentProblem = TerminalSessionOptions.FindEnvironmentProblem(request.Environment);
        if (environmentProblem is not null)
        {
            return new OmpTerminalLaunchFailed(environmentProblem);
        }

        var probe = await OmpVersionProbe.ProbeAsync(found.ExecutablePath, cancellationToken).ConfigureAwait(false);
        var version = probe is OmpVersionReported reported ? reported.Version : null;

        var options = new TerminalSessionOptions(
            found.ExecutablePath,
            request.Arguments,
            request.ProjectDirectory,
            request.Columns,
            request.Rows,
            request.Output)
        {
            Environment = request.Environment,
        };

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

    private static OmpLocation Located(string? executablePath) =>
        executablePath is null ? new OmpNotFound([], OmpLocator.InstallGuidance) : new OmpFound(executablePath);
}
