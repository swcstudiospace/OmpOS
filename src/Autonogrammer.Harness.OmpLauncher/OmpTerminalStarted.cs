using Autonogrammer.Harness.Terminal;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// omp was located and started in a terminal session. The result owns the session: disposing the result disposes the
/// session, which stops omp if it is still running.
/// </summary>
public sealed class OmpTerminalStarted : OmpTerminalLaunchResult, IAsyncDisposable
{
    private readonly ITerminalSession _session;

    /// <summary>Takes ownership of a running terminal session.</summary>
    /// <param name="executablePath">The path of the omp executable that was started.</param>
    /// <param name="version">The version omp reported, or null when the version probe did not produce one.</param>
    /// <param name="session">The running session. The result disposes it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="executablePath"/> or <paramref name="session"/> is null.</exception>
    public OmpTerminalStarted(string executablePath, string? version, ITerminalSession session)
    {
        ArgumentNullException.ThrowIfNull(executablePath);
        ArgumentNullException.ThrowIfNull(session);
        ExecutablePath = executablePath;
        Version = version;
        _session = session;
    }

    /// <summary>The path of the omp executable that was started.</summary>
    public string ExecutablePath { get; }

    /// <summary>The version omp reported, or null when the version probe did not produce one.</summary>
    public string? Version { get; }

    /// <summary>The running terminal session.</summary>
    public ITerminalSession Session => _session;

    /// <summary>Disposes the session.</summary>
    public ValueTask DisposeAsync() => _session.DisposeAsync();
}
