using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Rpc;

/// <summary>
/// A running <c>omp --mode rpc --no-ui --cwd &lt;cwd&gt;</c> child process as an <see cref="IRpcTransport"/>.
/// Stdout is read as newline-delimited frames capped at 1 MiB per physical line. Stderr is drained by a background
/// task into a bounded tail, so a chatty engine can never block the protocol stream. Closing stdin lets omp drain
/// accepted work and exit with status 0.
/// </summary>
public sealed class OmpRpcProcess : IRpcTransport
{
    private static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(10);
    private const int StderrTailLines = 200;

    private readonly Process _process;
    private readonly ConcurrentQueue<string> _stderrTail = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly Task _stderrDrain;
    private bool _inputClosed;
    private bool _disposed;

    private OmpRpcProcess(Process process)
    {
        _process = process;
        _stderrDrain = DrainStderrAsync(process.StandardError);
    }

    /// <summary>Gets the most recent stderr lines (up to 200), oldest first.</summary>
    public IReadOnlyList<string> RecentStderr => _stderrTail.ToArray();

    /// <summary>Starts <c>&lt;ompPath&gt; --mode rpc --no-ui --cwd &lt;cwd&gt;</c>.</summary>
    /// <param name="ompPath">The omp executable: an absolute path, or a name resolved through PATH.</param>
    /// <param name="cwd">The working directory the engine operates in.</param>
    /// <returns>The running process.</returns>
    /// <exception cref="ProcessStartHarnessException">The executable could not be started.</exception>
    public static OmpRpcProcess Start(string ompPath, string cwd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ompPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(cwd);

        var startInfo = new ProcessStartInfo
        {
            FileName = ompPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("rpc");
        startInfo.ArgumentList.Add("--no-ui");
        startInfo.ArgumentList.Add("--cwd");
        startInfo.ArgumentList.Add(cwd);

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception ex)
        {
            throw new ProcessStartHarnessException($"Could not start '{ompPath}'.", ex);
        }

        return process is null
            ? throw new ProcessStartHarnessException($"Could not start '{ompPath}'.")
            : new OmpRpcProcess(process);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        PipeReader reader = PipeReader.Create(_process.StandardOutput.BaseStream);
        try
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = result.Buffer;
                var lines = new List<string>();
                SequencePosition consumed = buffer.Start;
                while (buffer.PositionOf((byte)'\n') is { } newline)
                {
                    lines.Add(DecodeLine(buffer.Slice(buffer.Start, newline)));
                    buffer = buffer.Slice(buffer.GetPosition(1, newline));
                    consumed = buffer.Start;
                }

                reader.AdvanceTo(consumed, buffer.End);
                foreach (string line in lines)
                {
                    yield return line;
                }

                if (result.IsCompleted)
                {
                    if (buffer.Length > 0)
                    {
                        throw new MalformedFrameHarnessException("omp closed its output in the middle of a frame.");
                    }

                    yield break;
                }

                if (buffer.Length > OmpRpcFrames.MaxPhysicalLineContentBytes)
                {
                    throw new MalformedFrameHarnessException("A physical frame exceeds the 1 MiB transport limit.");
                }
            }
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task SendLineAsync(string line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(line);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_inputClosed)
            {
                throw new DisconnectedHarnessException("omp stdin is closed.");
            }

            StreamWriter stdin = _process.StandardInput;
            await stdin.WriteAsync(string.Concat(line, "\n").AsMemory(), cancellationToken).ConfigureAwait(false);
            await stdin.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            throw new DisconnectedHarnessException("Writing to omp stdin failed.", ex);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task CloseInputAsync(CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_inputClosed)
            {
                return;
            }

            _inputClosed = true;
            await _process.StandardInput.DisposeAsync().ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The engine already exited and its stdin pipe is gone; the close goal is met.
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return _process.ExitCode;
    }

    /// <summary>
    /// Closes stdin, waits up to ten seconds for the engine to exit on its own, then kills its process tree.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await CloseInputAsync(CancellationToken.None).ConfigureAwait(false);

        using var grace = new CancellationTokenSource(ShutdownGrace);
        try
        {
            await _process.WaitForExitAsync(grace.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (grace.IsCancellationRequested)
        {
            KillTree();
            await _process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        _process.Dispose();
        _writeGate.Dispose();
    }

    private static string DecodeLine(ReadOnlySequence<byte> bytes)
    {
        if (bytes.Length > OmpRpcFrames.MaxPhysicalLineContentBytes)
        {
            throw new MalformedFrameHarnessException("A physical frame exceeds the 1 MiB transport limit.");
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private void KillTree()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process exited between the grace timeout and the kill.
        }
    }

    private async Task DrainStderrAsync(StreamReader stderr)
    {
        try
        {
            while (await stderr.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                _stderrTail.Enqueue(line);
                while (_stderrTail.Count > StderrTailLines)
                {
                    _ = _stderrTail.TryDequeue(out _);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // The pipe closed with the process; the tail already holds everything it wrote.
        }
    }
}
