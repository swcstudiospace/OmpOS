using System.Diagnostics;
using System.Text;

namespace Autonogrammer.Harness.Terminal;

/// <summary>
/// Runs commands with <see cref="Process"/>: the arguments go through <c>ArgumentList</c>, the working directory is set
/// explicitly, and no shell is involved. Output is read as it arrives and only the first characters are kept, so a
/// chatty process cannot grow memory without bound.
/// </summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public async Task<ProcessOutcome> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        process.StandardInput.Close();

        var standardOutput = ReadBoundedAsync(process.StandardOutput, request.MaxOutputCharacters);
        var standardError = ReadBoundedAsync(process.StandardError, request.MaxOutputCharacters);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillTree(process);
            throw;
        }

        var (output, outputTruncated) = await standardOutput.ConfigureAwait(false);
        var (error, errorTruncated) = await standardError.ConfigureAwait(false);
        return new ProcessOutcome(process.ExitCode, output, error, outputTruncated, errorTruncated);
    }

    private static async Task<(string Text, bool Truncated)> ReadBoundedAsync(StreamReader reader, int maxCharacters)
    {
        var builder = new StringBuilder();
        var truncated = false;
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            var room = maxCharacters - builder.Length;
            if (room > 0)
            {
                builder.Append(buffer, 0, Math.Min(room, read));
            }

            if (read > room)
            {
                truncated = true;
            }
        }

        return (builder.ToString(), truncated);
    }

    private static void KillTree(Process process)
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
            // The process exited between the check and the kill. Nothing is left to stop.
        }
    }
}
