using System.ComponentModel;
using System.Diagnostics;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Real process runner for smoke tests (argv only, no shell).
/// </summary>
/// <remarks>
/// Delegates to the production <see cref="ProcessRunner"/> so the suite exercises the same
/// bounded-drain, whole-tree-kill behavior that ships (the timeout guards both process exit
/// and post-exit pipe drain, so no grandchild can hang the run).
/// </remarks>
internal sealed class RealProcessRunner : IProcessRunner
{
    private static readonly ProcessRunner Inner = new();

    /// <inheritdoc />
    public Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default)
    {
        return Inner.RunAsync(spec, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamStdoutLinesAsync(ProcessSpec spec, CancellationToken cancellationToken = default)
    {
        return Inner.StreamStdoutLinesAsync(spec, cancellationToken);
    }

    /// <summary>
    /// Checks whether a command exists on PATH.
    /// </summary>
    internal static async Task<bool> CheckCommandExistsAsync(string command, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.StartInfo.ArgumentList.Add(command);
            process.Start();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
