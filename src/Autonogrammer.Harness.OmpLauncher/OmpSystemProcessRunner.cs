using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// Runs managed-install commands as child processes. Standard input is closed at once, standard output and error are
/// kept as a bounded tail, and a process that exceeds its timeout is killed with its process tree. The child inherits the
/// environment, so proxy settings reach <c>bun install</c>.
/// </summary>
public sealed class OmpSystemProcessRunner : IOmpProcessRunner
{
    private const int TailCharacters = 16 * 1024;
    private static readonly TimeSpan ReaderGrace = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public async Task<OmpProcessResult> RunAsync(OmpProcessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var process = new Process { StartInfo = StartInfo(request) };
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new OmpProcessResult(OmpProcessOutcome.StartFailed, null, string.Empty, exception);
        }

        process.StandardInput.Close();
        var output = ReadTailAsync(process.StandardOutput);
        var error = ReadTailAsync(process.StandardError);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(request.Timeout);
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            _ = await CollectAsync(output, error).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new OmpProcessResult(OmpProcessOutcome.TimedOut, null, string.Empty, null);
        }

        var text = await CollectAsync(output, error).ConfigureAwait(false);
        return new OmpProcessResult(OmpProcessOutcome.Exited, process.ExitCode, text, null);
    }

    private static ProcessStartInfo StartInfo(OmpProcessRequest request)
    {
        var info = new ProcessStartInfo
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
            info.ArgumentList.Add(argument);
        }

        return info;
    }

    private static async Task<string> ReadTailAsync(StreamReader reader)
    {
        var builder = new StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            builder.Append(buffer, 0, read);
            if (builder.Length > 2 * TailCharacters)
            {
                builder.Remove(0, builder.Length - TailCharacters);
            }
        }

        return builder.Length > TailCharacters ? builder.ToString(builder.Length - TailCharacters, TailCharacters) : builder.ToString();
    }

    private static async Task<string> CollectAsync(Task<string> output, Task<string> error)
    {
        string[] texts;
        try
        {
            texts = await Task.WhenAll(output, error).WaitAsync(ReaderGrace).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or ObjectDisposedException)
        {
            return string.Empty;
        }

        var text = texts[0];
        var errorText = texts[1];
        var combined = errorText.Length == 0 ? text : text + "\n" + errorText;
        return combined.Length <= TailCharacters ? combined : combined[^TailCharacters..];
    }

    private static void Kill(Process process)
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
