using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
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
    /// <para>
    /// Streamed lines are bounded per line: <see cref="StreamStdoutLinesAsync"/> keeps at most
    /// <see cref="MaxStreamLineChars"/> characters per line and appends
    /// <see cref="StreamLineTruncatedMarker"/> when a line is truncated, so a very large record
    /// (or a missing newline) cannot grow host memory without bound.
    /// </para>
/// </remarks>
public sealed class ProcessRunner : IProcessRunner
{
    /// <summary>
    /// Exit code reported when <see cref="ProcessSpec.Timeout"/> elapses before process exit.
    /// </summary>
    internal const int TimeoutExitCode = 124;

    /// <summary>
    /// Maximum characters buffered for a single streamed stdout line (64 KiB).
    /// A longer line is truncated to this length with <see cref="StreamLineTruncatedMarker"/>
    /// appended; the excess characters are discarded up to the next newline. This bounds host
    /// memory when a child writes a very large record or never emits a newline.
    /// </summary>
    internal const int MaxStreamLineChars = 64 * 1024;

    /// <summary>
    /// Marker appended to a streamed line truncated at <see cref="MaxStreamLineChars"/>.
    /// </summary>
    internal const string StreamLineTruncatedMarker = "...[truncated]";

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

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamStdoutLinesAsync(ProcessSpec spec, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Argv.Count == 0)
        {
            throw new ArgumentException("ProcessSpec.Argv must contain at least the executable.", nameof(spec));
        }

        using var process = CreateProcess(spec);
        process.Start();
        var stderrTask = ReadCappedAsync(process.StandardError, cancellationToken);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(spec.Timeout);
        var lines = new BoundedLineReader(process.StandardOutput);
        try
        {
            while (true)
            {
                string? line;
                try
                {
                    line = await lines.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // spec.Timeout elapsed: stop following; the finally block terminates the tree.
                    break;
                }

                if (line is null)
                {
                    break;
                }

                yield return line;
            }
        }
        finally
        {
            // Runs on end of output, timeout, caller cancellation and early disposal by the consumer.
            KillTreeBestEffort(process);
            await WaitForExitBestEffortAsync(process, CancellationToken.None).ConfigureAwait(false);
            ObserveAbandoned(stderrTask);
        }
    }

    /// <summary>
    /// Newline-delimited reader that keeps at most <see cref="MaxStreamLineChars"/> characters per line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="TextReader.ReadLineAsync()"/> buffers a whole line before returning, so a child
    /// that writes a very large record (or never emits a newline) makes the host retain the entire
    /// line. This reader consumes bounded chunks instead, discards the excess of an oversized line
    /// up to its newline, and appends <see cref="StreamLineTruncatedMarker"/> so truncation is visible.
    /// </para>
    /// </remarks>
    private sealed class BoundedLineReader
    {
        private readonly TextReader _reader;
        private string? _carry;

        public BoundedLineReader(TextReader reader)
        {
            _reader = reader;
        }

        public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();
            var truncated = false;
            if (ConsumeCarry(builder, ref truncated))
            {
                return FinishLine(builder, truncated);
            }

            var buffer = new char[8192];
            while (true)
            {
                int read = await _reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return builder.Length == 0 && !truncated ? null : FinishLine(builder, truncated);
                }

                int newline = Array.IndexOf(buffer, '\n', 0, read);
                if (newline < 0)
                {
                    AppendChunk(builder, buffer.AsSpan(0, read), ref truncated);
                    continue;
                }

                int end = newline;
                if (end > 0 && buffer[end - 1] == '\r')
                {
                    end--;
                }

                AppendChunk(builder, buffer.AsSpan(0, end), ref truncated);
                _carry = newline + 1 < read ? new string(buffer, newline + 1, read - newline - 1) : string.Empty;
                return FinishLine(builder, truncated);
            }
        }

        private bool ConsumeCarry(StringBuilder builder, ref bool truncated)
        {
            if (_carry is null)
            {
                return false;
            }

            string carry = _carry;
            _carry = null;
            int newline = carry.AsSpan().IndexOf('\n');
            if (newline < 0)
            {
                AppendChunk(builder, carry.AsSpan(), ref truncated);
                return false;
            }

            int end = newline;
            if (end > 0 && carry[end - 1] == '\r')
            {
                end--;
            }

            AppendChunk(builder, carry.AsSpan(0, end), ref truncated);
            _carry = carry[(newline + 1)..];
            return true;
        }

        private static void AppendChunk(StringBuilder builder, ReadOnlySpan<char> chunk, ref bool truncated)
        {
            int remaining = MaxStreamLineChars - builder.Length;
            if (remaining <= 0)
            {
                truncated = true;
                return;
            }

            if (chunk.Length > remaining)
            {
                builder.Append(chunk[..remaining]);
                truncated = true;
            }
            else
            {
                builder.Append(chunk);
            }
        }

        private static string FinishLine(StringBuilder builder, bool truncated)
        {
            string line = builder.ToString();
            if (!truncated)
            {
                return line;
            }

            if (line.Length > 0 && char.IsHighSurrogate(line[^1]))
            {
                line = line[..^1];
            }

            return line + StreamLineTruncatedMarker;
        }
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
        try
        {
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller cancellation must not leave the child running: kill and reap the tree,
            // abandon the pipes, then rethrow.
            await CancelChildAsync(process, stdoutTask, stderrTask).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task CancelChildAsync(Process process, Task<(string Output, bool Truncated)> stdoutTask, Task<(string Output, bool Truncated)> stderrTask)
    {
        KillTreeBestEffort(process);
        await WaitForExitBestEffortAsync(process, CancellationToken.None).ConfigureAwait(false);
        ObserveAbandoned(stdoutTask);
        ObserveAbandoned(stderrTask);
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
        int rootId;
        try
        {
            rootId = process.Id;
        }
        catch (InvalidOperationException)
        {
            // Never started or already disposed; nothing to kill.
            return;
        }
        catch (Win32Exception)
        {
            // Process id unavailable; nothing to kill.
            return;
        }
        catch (NotSupportedException)
        {
            // Platform without process ids; nothing to kill.
            return;
        }

        // Snapshot the whole descendant tree BEFORE killing anything. Killing the
        // root first orphans grandchildren to init, after which they are no longer
        // discoverable by parent id. Every pid below is killed by id, so later
        // re-parenting cannot hide a snapshotted process from its SIGKILL.
        foreach (int pid in SnapshotDescendants(rootId))
        {
            TryKillPid(pid);
        }

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

    /// <summary>
    /// Snapshots the live descendant pids of <paramref name="rootPid"/> without killing anything.
    /// </summary>
    /// <param name="rootPid">The child process id to walk down from.</param>
    /// <returns>Every descendant pid found via /proc at snapshot time, root excluded.</returns>
    private static List<int> SnapshotDescendants(int rootPid)
    {
        Dictionary<int, List<int>> children = BuildChildrenMap();
        var descendants = new List<int>();
        var pending = new Queue<int>();
        pending.Enqueue(rootPid);
        while (pending.TryDequeue(out int parent))
        {
            if (!children.TryGetValue(parent, out List<int>? direct))
            {
                continue;
            }

            foreach (int child in direct)
            {
                descendants.Add(child);
                pending.Enqueue(child);
            }
        }

        return descendants;
    }

    /// <summary>
    /// Builds a parent-pid to child-pids map from a single /proc scan.
    /// </summary>
    /// <returns>The parent-to-children map; empty off Linux or when /proc is unreadable.</returns>
    private static Dictionary<int, List<int>> BuildChildrenMap()
    {
        var children = new Dictionary<int, List<int>>();
        if (!OperatingSystem.IsLinux())
        {
            return children;
        }

        string[] entries;
        try
        {
            entries = Directory.GetDirectories("/proc");
        }
        catch (IOException)
        {
            // /proc unreadable; the caller falls back to the framework tree kill.
            return children;
        }
        catch (UnauthorizedAccessException)
        {
            // /proc unreadable; the caller falls back to the framework tree kill.
            return children;
        }

        foreach (string entry in entries)
        {
            if (!int.TryParse(Path.GetFileName(entry), CultureInfo.InvariantCulture, out int pid))
            {
                continue;
            }

            if (!TryReadParentPid(pid, out int parentPid))
            {
                continue;
            }

            if (!children.TryGetValue(parentPid, out List<int>? direct))
            {
                direct = new List<int>();
                children[parentPid] = direct;
            }

            direct.Add(pid);
        }

        return children;
    }

    /// <summary>
    /// Reads the parent pid of <paramref name="pid"/> from /proc.
    /// </summary>
    /// <param name="pid">The process id to inspect.</param>
    /// <param name="parentPid">The parent pid on success; zero otherwise.</param>
    /// <returns>True when the parent pid was read.</returns>
    private static bool TryReadParentPid(int pid, out int parentPid)
    {
        parentPid = 0;
        string stat;
        try
        {
            stat = File.ReadAllText($"/proc/{pid}/stat");
        }
        catch (IOException)
        {
            // Exited mid-scan or unreadable; skipped from the snapshot.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // Unreadable; skipped from the snapshot.
            return false;
        }

        // Layout is "pid (comm) state ppid ..."; comm may itself contain spaces
        // or parens, so the fields after the final ')' are split instead.
        int close = stat.LastIndexOf(')');
        if (close < 0)
        {
            return false;
        }

        string[] fields = stat[(close + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > 1 && int.TryParse(fields[1], CultureInfo.InvariantCulture, out parentPid);
    }

    /// <summary>
    /// SIGKILLs one pid best-effort; never throws.
    /// </summary>
    /// <param name="pid">The process id to kill.</param>
    private static void TryKillPid(int pid)
    {
        try
        {
            using Process probe = Process.GetProcessById(pid);
            if (!probe.HasExited)
            {
                probe.Kill();
            }
        }
        catch (ArgumentException)
        {
            // Exited between the snapshot and the kill.
        }
        catch (InvalidOperationException)
        {
            // Exited between the snapshot and the kill.
        }
        catch (Win32Exception)
        {
            // Already exiting; the root kill still runs afterwards.
        }
        catch (NotSupportedException)
        {
            // Platform without kill support.
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
