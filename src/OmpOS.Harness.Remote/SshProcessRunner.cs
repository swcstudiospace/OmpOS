using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace OmpOS.Harness.Remote;

/// <summary>
/// Runs a short ssh command to completion and captures its output under a size cap and a time limit. The process is started without a
/// shell from an argument list. It is used for <c>ssh -G</c>, <c>ssh -V</c> and the preflight, never for a session.
/// </summary>
internal sealed class SshProcessRunner
{
    private const int MaxCaptureCharacters = 1024 * 1024;
    private static readonly TimeSpan DrainGrace = TimeSpan.FromSeconds(5);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly TimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="SshProcessRunner"/> class.</summary>
    /// <param name="clock">The clock the time limit uses, or <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public SshProcessRunner(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    /// <summary>Runs an invocation, writes its preamble (if any), closes stdin and waits.</summary>
    /// <param name="invocation">What to run.</param>
    /// <param name="timeout">The time limit; the process is killed when it is exceeded.</param>
    /// <param name="cancellationToken">Cancels the run and kills the process.</param>
    /// <returns>The outcome. A process that cannot start is reported in <see cref="SshProcessResult.StartFailure"/>, not thrown.</returns>
    public async Task<SshProcessResult> RunAsync(SshInvocation invocation, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var startInfo = new ProcessStartInfo
        {
            FileName = invocation.FileName,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
        };
        foreach (var argument in invocation.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = TryStart(startInfo, out var startFailure);
        if (process is null)
        {
            return new SshProcessResult(-1, string.Empty, string.Empty, TimedOut: false, startFailure);
        }

        var input = WriteInputAsync(process.StandardInput.BaseStream, invocation.Preamble);
        var output = ReadCappedAsync(process.StandardOutput);
        var error = ReadCappedAsync(process.StandardError);

        using var limit = new CancellationTokenSource(timeout, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            Kill(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }

        try
        {
            await Task.WhenAll(input, output, error).WaitAsync(DrainGrace, _clock, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A grandchild (a connection-sharing master) can keep the pipes open after the client exited; the text read so far is enough.
        }

        var standardOutput = output.IsCompletedSuccessfully ? await output.ConfigureAwait(false) : string.Empty;
        var standardError = error.IsCompletedSuccessfully ? await error.ConfigureAwait(false) : string.Empty;
        return new SshProcessResult(timedOut ? -1 : process.ExitCode, standardOutput, standardError, timedOut, StartFailure: null);
    }

    private static Process? TryStart(ProcessStartInfo startInfo, out string? failure)
    {
        failure = null;
        try
        {
            var started = Process.Start(startInfo);
            if (started is null)
            {
                failure = "The process did not start.";
            }

            return started;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or UnauthorizedAccessException)
        {
            failure = ex.Message;
            return null;
        }
    }

    private static async Task WriteInputAsync(Stream stdin, SshStdinPreamble? preamble)
    {
        try
        {
            if (preamble is not null)
            {
                await stdin.WriteAsync(preamble.Bytes, CancellationToken.None).ConfigureAwait(false);
                await stdin.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }

            stdin.Close();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // The child exited before reading its input; its exit code and stderr explain why.
            _ = ex;
        }
    }

    private static async Task<string> ReadCappedAsync(StreamReader reader)
    {
        var builder = new StringBuilder();
        var buffer = new char[4096];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false)) > 0)
            {
                // Keep draining past the cap so the child never blocks on a full pipe.
                var room = MaxCaptureCharacters - builder.Length;
                if (room > 0)
                {
                    builder.Append(buffer, 0, Math.Min(read, room));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // A killed child ends the pipe abruptly; what was read is kept.
            _ = ex;
        }

        return builder.ToString();
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
            // Exited between the check and the kill.
        }
    }
}
