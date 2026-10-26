using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Rpc;

/// <summary>Runs <c>omp --version</c> and parses the result. Never starts an RPC session.</summary>
public static class OmpVersionProbe
{
    private const int MaxOutputCharacters = 4096;

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
        var (output, overflowed) = await ReadProbeOutputAsync(process, cancellationToken).ConfigureAwait(false);
        if (overflowed)
        {
            throw new ProcessStartHarnessException($"'{ompPath} --version' printed more than {MaxOutputCharacters} characters.");
        }

        if (process.ExitCode != 0)
        {
            throw new ProcessStartHarnessException($"'{ompPath} --version' exited with code {process.ExitCode}.");
        }

        return OmpVersion.Parse(output);
    }

    // Reads both streams concurrently so neither can fill and block the child, and returns only after the child has
    // exited and both reads have finished. Stdout is capped: output past the cap kills the child.
    private static async Task<(string Text, bool Overflowed)> ReadProbeOutputAsync(Process process, CancellationToken cancellationToken)
    {
        Task<(string Text, bool Overflowed)> stdout = ReadBoundedStdoutAsync(process);
        Task<string> stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // However the wait ends, the child must not outlive the probe, and both reads must finish before the caller disposes the process.
            if (!process.HasExited)
            {
                KillQuietly(process);
            }

            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        }

        return await stdout.ConfigureAwait(false);
    }

    private static Process StartProbe(ProcessStartInfo startInfo, string ompPath)
    {
        try
        {
            return Process.Start(startInfo)
                ?? throw new ProcessStartHarnessException($"Could not start '{ompPath}'.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or UnauthorizedAccessException)
        {
            throw new ProcessStartHarnessException($"Could not start '{ompPath}'.", ex);
        }
    }

    // Reads stdout to its end. Output past the cap kills the child and stops the read; the caller reports the overflow.
    private static async Task<(string Text, bool Overflowed)> ReadBoundedStdoutAsync(Process process)
    {
        var builder = new StringBuilder();
        var buffer = new char[1024];
        int read;
        while ((read = await process.StandardOutput.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            if (builder.Length + read > MaxOutputCharacters)
            {
                KillQuietly(process);
                return (string.Empty, true);
            }

            builder.Append(buffer, 0, read);
        }

        return (builder.ToString(), false);
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
