using System.Diagnostics;

namespace Autonogrammer.Harness.Herdr;

/// <summary>
/// Runs processes with <see cref="Process"/>. Arguments go through <see cref="ProcessStartInfo.ArgumentList"/>, so no
/// shell parses them.
/// </summary>
public sealed class HerdrSystemProcessRunner : IHerdrProcessRunner
{
    /// <summary>Runs the executable, waits for it, and returns its exit code and output.</summary>
    /// <param name="executablePath">The path of the executable to run.</param>
    /// <param name="arguments">The arguments, one element each.</param>
    /// <param name="cancellationToken">Cancels the wait. The process is killed with its child tree when this fires.</param>
    /// <returns>The exit code and both output streams.</returns>
    public async Task<HerdrProcessResult> RunAsync(string executablePath, string[] arguments, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("The herdr process could not be started.");
        }

        process.StandardInput.Close();
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            throw;
        }

        return new HerdrProcessResult(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the check and the kill, so there is nothing left to stop.
        }
    }
}
