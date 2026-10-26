using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using OmpOS.Harness.OmpLauncher;

namespace OmpOS.Harness.OmpConfig;

/// <summary>
/// Runs <c>omp &lt;arguments&gt;</c> for the configuration subcommands. The executable is found through
/// <see cref="OmpLocator"/>; the child environment is the host environment with <c>PI_CODING_AGENT_DIR</c> (and <c>HOME</c>
/// when configured) forced to the options, so the credentials the host exports still reach omp. Output is bounded, the process
/// tree is killed when the time limit passes or the caller cancels, and nothing goes through a shell.
/// </summary>
/// <remarks>
/// Verified against omp 18.12.6: <c>omp token</c>, <c>omp config get|set</c> and <c>omp auth import</c> load no extension.
/// <c>omp models</c> does discover and load the extensions of the agent directory, so its callers add <c>--no-extensions</c>.
/// </remarks>
public sealed class OmpCommandRunner
{
    private const string AgentDirectoryVariable = "PI_CODING_AGENT_DIR";
    private const string HomeVariable = "HOME";
    private const int ReadBufferBytes = 8192;

    private readonly OmpConfigOptions _options;

    /// <summary>Creates a runner.</summary>
    /// <param name="options">The omp location, agent directory and limits.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The timeout or the output limit is not positive.</exception>
    public OmpCommandRunner(OmpConfigOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.CommandTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.CommandTimeout, "The command timeout must be positive.");
        }

        if (options.MaxOutputBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxOutputBytes, "The output limit must be positive.");
        }

        _options = options;
    }

    /// <summary>
    /// Runs one omp invocation. omp not being installed, not being runnable or not starting is reported in
    /// <see cref="OmpCommandResult.StartFailure"/>; a timeout is reported in <see cref="OmpCommandResult.TimedOut"/>.
    /// </summary>
    /// <param name="request">The arguments and optional standard input.</param>
    /// <param name="cancellationToken">Cancels the invocation. The process tree is killed and the cancellation is thrown.</param>
    /// <returns>What the invocation produced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<OmpCommandResult> RunAsync(OmpCommandRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var carriedInput = request.StandardInput.HasValue;
        var location = OmpLocator.Locate(_options.Locator ?? OmpLocatorOptions.FromEnvironment(null));
        if (location is not OmpFound found)
        {
            return StartFailed(OmpLocator.InstallGuidance, carriedInput);
        }

        using var process = new Process { StartInfo = BuildStartInfo(found.ExecutablePath, request) };
        try
        {
            _ = process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            return StartFailed($"The omp executable could not be started: {exception.Message}", carriedInput);
        }

        using var timeoutSource = new CancellationTokenSource(_options.CommandTimeout, _options.Clock());
        using var limitSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        var token = limitSource.Token;

        var output = new BoundedBuffer(_options.MaxOutputBytes, request.CaptureStandardOutput);
        var error = new BoundedBuffer(_options.MaxOutputBytes, keep: true);
        var standardOutput = ReadIntoAsync(process.StandardOutput.BaseStream, output);
        var standardError = ReadIntoAsync(process.StandardError.BaseStream, error);
        try
        {
            await WriteStandardInputAsync(process, request.StandardInput, token).ConfigureAwait(false);
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            await standardOutput.WaitAsync(token).ConfigureAwait(false);
            await standardError.WaitAsync(token).ConfigureAwait(false);
            return Finished(process.ExitCode, output, error, timedOut: false, carriedInput);
        }
        catch (OperationCanceledException)
        {
            KillTree(process);
            await AwaitReadsAfterKillAsync(standardOutput, standardError).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return Finished(-1, output, error, timedOut: true, carriedInput);
        }
    }

    private ProcessStartInfo BuildStartInfo(string executablePath, OmpCommandRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetTempPath(),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment[AgentDirectoryVariable] = _options.ResolveAgentDirectory();
        if (!string.IsNullOrWhiteSpace(_options.Home))
        {
            startInfo.Environment[HomeVariable] = _options.Home;
        }

        return startInfo;
    }

    private static OmpCommandResult Finished(int exitCode, BoundedBuffer output, BoundedBuffer error, bool timedOut, bool carriedInput)
    {
        var (outputText, outputTruncated) = output.Snapshot();
        var (errorText, errorTruncated) = error.Snapshot();
        return new OmpCommandResult(exitCode, outputText, errorText, timedOut, outputTruncated || errorTruncated)
        {
            CarriedStandardInput = carriedInput,
        };
    }

    private static OmpCommandResult StartFailed(string reason, bool carriedInput) =>
        new(-1, string.Empty, string.Empty, TimedOut: false, OutputTruncated: false)
        {
            StartFailure = reason,
            CarriedStandardInput = carriedInput,
        };

    private static async Task WriteStandardInputAsync(Process process, ReadOnlyMemory<byte>? input, CancellationToken cancellationToken)
    {
        var stream = process.StandardInput.BaseStream;
        try
        {
            if (input is { } bytes)
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (IOException)
        {
            // The process closed its input before reading all of it. Its exit code and error say why.
        }
        finally
        {
            try
            {
                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // The pipe is already closed by the process side.
            }
        }
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
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            // The process exited between the check and the kill. Nothing is left to stop.
        }
    }

    // After a kill the pipes close and both reads finish at once. A grandchild that escaped the tree could keep a pipe open,
    // so the wait is bounded and its failure ignored: the caller already has its answer.
    private static async Task AwaitReadsAfterKillAsync(Task standardOutput, Task standardError)
    {
        try
        {
            await Task.WhenAll(standardOutput, standardError).WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or TimeoutException or IOException or ObjectDisposedException)
        {
            // Nothing more can be read from a killed process.
        }
    }

    private static async Task ReadIntoAsync(Stream stream, BoundedBuffer buffer)
    {
        var chunk = new byte[ReadBufferBytes];
        try
        {
            int read;
            while ((read = await stream.ReadAsync(chunk).ConfigureAwait(false)) > 0)
            {
                buffer.Append(chunk, read);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // The pipe closed under the read; what was read stands.
        }
    }

    /// <summary>What a stream produced so far, capped at a byte limit. A timeout or a kill still reads what arrived before it.</summary>
    private sealed class BoundedBuffer
    {
        private readonly Lock _gate = new();
        private readonly ArrayBufferWriter<byte> _kept = new();
        private readonly int _limit;
        private readonly bool _keep;
        private bool _truncated;

        public BoundedBuffer(int limit, bool keep)
        {
            _limit = limit;
            _keep = keep;
        }

        public void Append(byte[] chunk, int count)
        {
            if (!_keep)
            {
                return;
            }

            lock (_gate)
            {
                var room = _limit - _kept.WrittenCount;
                if (room > 0)
                {
                    _kept.Write(chunk.AsSpan(0, Math.Min(room, count)));
                }

                _truncated |= count > room;
            }
        }

        public (string Text, bool Truncated) Snapshot()
        {
            lock (_gate)
            {
                return (Encoding.UTF8.GetString(_kept.WrittenSpan), _truncated);
            }
        }
    }
}
