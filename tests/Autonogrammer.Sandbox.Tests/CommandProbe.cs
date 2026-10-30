using System.ComponentModel;
using System.Diagnostics;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Probes whether external commands are available for environment-gated tests.
/// </summary>
internal static class CommandProbe
{
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
