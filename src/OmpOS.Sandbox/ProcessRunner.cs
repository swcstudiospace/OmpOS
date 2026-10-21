using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace OmpOS.Sandbox;

/// <summary>
/// Production <see cref="IProcessRunner"/>: argv-only process invocation with enforced timeouts.
/// </summary>
/// <remarks>
/// <para>
/// Arguments travel via <see cref="ProcessStartInfo.ArgumentList"/> (no shell, no manual
/// quoting), so values containing spaces, quotes, or newlines cannot split or merge.
/// </para>
/// <para>
/// Both timeouts are enforced: <see cref="ProcessSpec.Timeout"/> guards process exit, and a
/// bounded drain budget guards the post-exit stdout/stderr drain. A grandchild that inherits
/// the pipes (daemons, log tailers) cannot hang the caller forever: on drain timeout the
/// process tree is killed best-effort, the pipe readers are abandoned with observed
/// continuations, and a timed-out result is returned.
/// </para>
/// <para>
/// Captured output is bounded: each stream keeps at most <see cref="MaxOutputChars"/>
/// characters and the result is marked <see cref="ProcessResult.Truncated"/> when the cap
/// is hit. Excess output is discarded while the drain continues so a verbose child never
/// blocks on a full pipe.
/// </para>
/// </remarks>
public sealed class ProcessRunner : IProcessRunner
{
    /// <summary>
    /// Exit code reported when <see cref="ProcessSpec.Timeout"/> elapses before process exit.
    /// </summary>
    internal const int TimeoutExitCode = 124;

    /// <summary>
    /// Maximum captured output per stream (1 MiB, measured in characters; identical to bytes for single-byte output).
    /// </summary>
    internal const int MaxOutputChars = 1024 * 1024;

    private static readonly TimeSpan MinDrainBudget = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxDrainBudget = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan KillWaitBudget = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Argv.Count == 0)
        {
            throw new ArgumentException("ProcessSpec.Argv must contain at least the executable.", nameof(spec));
        }

        using var process = CreateProcess(spec);
        process.Start();
        return await ExecuteAsync(process, spec, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Computes the bounded post-exit pipe-drain budget from the process timeout.
    /// </summary>
    /// <param name="timeout">The process timeout from the spec.</param>
    /// <returns>A drain budget of one tenth of the timeout, clamped to 1s..30s.</returns>
    internal static TimeSpan DrainBudget(TimeSpan timeout)
    {
        return TimeSpan.FromTicks(Math.Clamp(timeout.Ticks / 10, MinDrainBudget.Ticks, MaxDrainBudget.Ticks));
    }

    private static Process CreateProcess(ProcessSpec spec)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = spec.Argv[0],
                WorkingDirectory = spec.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var arg in spec.Argv.Skip(1))
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in spec.Environment)
        {
            process.StartInfo.Environment[key] = value;
        }

        return process;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort kill/wait during timeout handling must never mask the timeout result; failures are swallowed deliberately after the tree kill is attempted.")]
    private static async Task<ProcessResult> ExecuteAsync(Process process, ProcessSpec spec, CancellationToken cancellationToken)
    {
        var stdoutTask = ReadCappedAsync(process.StandardOutput, cancellationToken);
        var stderrTask = ReadCappedAsync(process.StandardError, cancellationToken);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(spec.Timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // spec.Timeout elapsed (not caller cancellation): kill the whole tree and
            // abandon the pipes. Never await a pipe after the process is gone.
            return await HandleExitTimeoutAsync(process, stdoutTask, stderrTask, cancellationToken).ConfigureAwait(false);
        }

        return await DrainAsync(process, stdoutTask, stderrTask, DrainBudget(spec.Timeout), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ProcessResult> HandleExitTimeoutAsync(Process process, Task<(string Output, bool Truncated)> stdoutTask, Task<(string Output, bool Truncated)> stderrTask, CancellationToken cancellationToken)
    {
        KillTreeBestEffort(process);
        await WaitForExitBestEffortAsync(process, cancellationToken).ConfigureAwait(false);
        ObserveAbandoned(stdoutTask);
        ObserveAbandoned(stderrTask);
        return new ProcessResult
        {
            ExitCode = TimeoutExitCode,
            Stdout = string.Empty,
            Stderr = string.Empty,
            TimedOut = true,
        };
    }

    private static async Task<ProcessResult> DrainAsync(Process process, Task<(string Output, bool Truncated)> stdoutTask, Task<(string Output, bool Truncated)> stderrTask, TimeSpan drainBudget, CancellationToken cancellationToken)
    {
        using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drainCts.CancelAfter(drainBudget);
        (string Output, bool Truncated) stdout;
        (string Output, bool Truncated) stderr;
        try
        {
            stdout = await stdoutTask.WaitAsync(drainCts.Token).ConfigureAwait(false);
            stderr = await stderrTask.WaitAsync(drainCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Process exited but a lingering child holds the pipes open: kill best-effort,
            // abandon the readers (observed), and report the timeout. The real exit code is
            // preserved when the process itself exited.
            KillTreeBestEffort(process);
            ObserveAbandoned(stdoutTask);
            ObserveAbandoned(stderrTask);
            return new ProcessResult
            {
                ExitCode = process.HasExited ? process.ExitCode : TimeoutExitCode,
                Stdout = string.Empty,
                Stderr = string.Empty,
                TimedOut = true,
            };
        }

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            Stdout = stdout.Output,
            Stderr = stderr.Output,
            TimedOut = false,
            Truncated = stdout.Truncated || stderr.Truncated,
        };
    }

    /// <summary>
    /// Reads a pipe to end, keeping at most <see cref="MaxOutputChars"/> characters.
    /// Excess output is discarded while the drain continues so a verbose child never blocks on a full pipe.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The kept output and whether the cap was hit.</returns>
    private static async Task<(string Output, bool Truncated)> ReadCappedAsync(TextReader reader, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var truncated = false;
        var buffer = new char[8192];
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            var remaining = MaxOutputChars - builder.Length;
            if (remaining <= 0)
            {
                truncated = true;
                continue;
            }

            if (read > remaining)
            {
                builder.Append(buffer, 0, remaining);
                truncated = true;
            }
            else
            {
                builder.Append(buffer, 0, read);
            }
        }

        var output = builder.ToString();
        if (truncated && output.Length > 0 && char.IsHighSurrogate(output[^1]))
        {
            output = output[..^1];
        }

        return (output, truncated);
    }

    private static void KillTreeBestEffort(Process process)
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
            // Already exited between the wait and the kill; still a timeout.
        }
        catch (Win32Exception)
        {
            // Process could not be killed (already exiting); still a timeout.
        }
        catch (NotSupportedException)
        {
            // Platform without tree kill; still a timeout.
        }
    }

    private static async Task WaitForExitBestEffortAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            waitCts.CancelAfter(KillWaitBudget);
            await process.WaitForExitAsync(waitCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Kill wait budget elapsed; the pipes are abandoned below regardless.
        }
        catch (InvalidOperationException)
        {
            // Process already exited; nothing to wait for.
        }
    }

    private static void ObserveAbandoned(Task task)
    {
        _ = task.ContinueWith(
            static t => t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
