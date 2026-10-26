using System.Diagnostics;

namespace OmpOS.Harness.OmpLauncher;

/// <summary>
/// A running omp process. Standard input, output and error are redirected to the caller. Disposing the session kills
/// the process tree if it is still running.
/// </summary>
public sealed class OmpSession : IDisposable
{
    private readonly Process _process;

    internal OmpSession(Process process)
    {
        _process = process;
    }

    /// <summary>The operating system process identifier.</summary>
    public int ProcessId => _process.Id;

    /// <summary>The redirected standard input.</summary>
    public TextWriter StandardInput => _process.StandardInput;

    /// <summary>The redirected standard output.</summary>
    public TextReader StandardOutput => _process.StandardOutput;

    /// <summary>The redirected standard error.</summary>
    public TextReader StandardError => _process.StandardError;

    /// <summary>
    /// Waits for the process to exit. Cancelling the wait does not stop the process.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return _process.ExitCode;
    }

    /// <summary>
    /// Kills the process and its children. Does nothing when the process has already exited.
    /// </summary>
    public void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the check and the kill. Nothing is left to stop.
        }
    }

    /// <summary>
    /// Kills the process tree if it is still running, then releases the process handle.
    /// </summary>
    public void Dispose()
    {
        Kill();
        _process.Dispose();
    }
}
