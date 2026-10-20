using System.ComponentModel;
using System.Diagnostics;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Rpc;

/// <summary>Runs <c>omp --version</c> and parses the result. Never starts an RPC session.</summary>
public static class OmpVersionProbe
{
    /// <summary>Runs <c>&lt;ompPath&gt; --version</c>.</summary>
    /// <param name="ompPath">The omp executable: an absolute path, or a name resolved through PATH.</param>
    /// <param name="cancellationToken">Cancels the probe; the child process is killed on cancellation.</param>
    /// <returns>The installed version.</returns>
    /// <exception cref="ProcessStartHarnessException">The executable could not start, exited non-zero, or printed no version.</exception>
    public static async Task<OmpVersion> ProbeAsync(string ompPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ompPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = ompPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--version");

        using Process process = StartProbe(startInfo, ompPath);
        // Both streams are read to completion concurrently so neither can fill and block the child.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillQuietly(process);
            throw;
        }

        string output = await stdout.ConfigureAwait(false);
        _ = await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new ProcessStartHarnessException($"'{ompPath} --version' exited with code {process.ExitCode}.");
        }

        return OmpVersion.Parse(output);
    }

    private static Process StartProbe(ProcessStartInfo startInfo, string ompPath)
    {
        try
        {
            return Process.Start(startInfo)
                ?? throw new ProcessStartHarnessException($"Could not start '{ompPath}'.");
        }
        catch (Win32Exception ex)
        {
            throw new ProcessStartHarnessException($"Could not start '{ompPath}'.", ex);
        }
    }

    private static void KillQuietly(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The probe already exited.
        }
    }
}
