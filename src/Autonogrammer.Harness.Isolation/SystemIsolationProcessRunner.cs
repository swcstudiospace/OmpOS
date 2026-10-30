using System.ComponentModel;
using System.Diagnostics;

namespace Autonogrammer.Harness.Isolation;

/// <summary>Starts a process by argument vector. The live probe uses it. Unit tests use a fake and never call this type.</summary>
public sealed class SystemIsolationProcessRunner : IIsolationProcessRunner
{
    /// <inheritdoc />
    public async Task<IsolationProcessRun> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("The process did not start.");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
            return new IsolationProcessRun(127, string.Empty, false, true);
        }

        using (process)
        {
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                return new IsolationProcessRun(-1, string.Empty, true, false);
            }

            var output = await stdout.ConfigureAwait(false) + await stderr.ConfigureAwait(false);
            return new IsolationProcessRun(process.ExitCode, output, false, false);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process already exited.
        }
    }
}
