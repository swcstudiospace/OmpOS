using System.ComponentModel;
using System.Diagnostics;

namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Runs hook processes with <see cref="Process"/>. A shell string goes to <c>/bin/sh -c</c> (Linux and macOS) or
/// PowerShell (Windows); the <c>args</c> form starts the executable directly with an argument list and no shell. The JSON
/// payload is written to standard input on its own task and the pipe is closed, so a hook that never reads it cannot hang
/// the host. The child's environment is exactly <see cref="HookInvocation.Environment"/>. Standard output and error are
/// drained to the end and only the first bytes up to the bound are kept; truncation is reported, never turned into a failure.
/// Cancellation kills the whole process tree.
/// </summary>
public sealed class SystemHookCommandRunner : IHookCommandRunner
{
    private const int ReadChunkBytes = 8192;

    /// <inheritdoc />
    public async Task<HookCommandOutcome> RunAsync(HookInvocation invocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        if (invocation.MaxStandardOutputBytes <= 0 || invocation.MaxStandardErrorBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(invocation), "The output bounds must be positive.");
        }

        if (!Directory.Exists(invocation.WorkingDirectory))
        {
            return Failed(HookSpawnFailure.WorkingDirectoryMissing, $"The working directory '{invocation.WorkingDirectory}' does not exist.");
        }

        var startInfo = CreateStartInfo(invocation);
        if (startInfo is null)
        {
            return Failed(HookSpawnFailure.Other, "The invocation names neither a command nor an argument list.");
        }

        using var process = new Process { StartInfo = startInfo };
        var startFailure = TryStart(process);
        if (startFailure is not null)
        {
            return startFailure;
        }

        var standardInput = WriteInputAsync(process.StandardInput.BaseStream, invocation.StandardInput);
        var standardOutput = CaptureAsync(process.StandardOutput.BaseStream, invocation.MaxStandardOutputBytes);
        var standardError = CaptureAsync(process.StandardError.BaseStream, invocation.MaxStandardErrorBytes);

        var completed = false;
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            // The pipes close when every writer exits. A descendant that outlives the hook and keeps a pipe open is bounded by
            // the caller's cancellation, which is the hook timeout.
            var (outputBytes, outputTruncated) = await standardOutput.WaitAsync(cancellationToken).ConfigureAwait(false);
            var (errorBytes, errorTruncated) = await standardError.WaitAsync(cancellationToken).ConfigureAwait(false);
            var delivered = await standardInput.WaitAsync(cancellationToken).ConfigureAwait(false);
            completed = true;
            return new HookCommandOutcome(
                process.ExitCode,
                HookOutput.Decode(outputBytes, outputTruncated),
                HookOutput.Decode(errorBytes, errorTruncated),
                outputTruncated,
                errorTruncated,
                StandardInputDelivered: delivered);
        }
        finally
        {
            if (!completed)
            {
                ProcessTree.Kill(process);
                await TaskFault.AwaitAsync(Task.WhenAll(standardInput, standardOutput, standardError)).ConfigureAwait(false);
            }
        }
    }

    private static ProcessStartInfo? CreateStartInfo(HookInvocation invocation)
    {
        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = invocation.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (invocation.Shell == HookShell.None)
        {
            if (invocation.Args is not { Count: > 0 })
            {
                return null;
            }

            startInfo.FileName = invocation.Args[0];
            for (var i = 1; i < invocation.Args.Count; i++)
            {
                startInfo.ArgumentList.Add(invocation.Args[i]);
            }
        }
        else
        {
            if (invocation.Command is null)
            {
                return null;
            }

            AddShell(startInfo, invocation.Shell, invocation.Command);
        }

        startInfo.Environment.Clear();
        foreach (var pair in invocation.Environment)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        return startInfo;
    }

    private static void AddShell(ProcessStartInfo startInfo, HookShell shell, string command)
    {
        switch (shell)
        {
            case HookShell.PowerShell:
                startInfo.FileName = "powershell.exe";
                startInfo.ArgumentList.Add("-NoLogo");
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-NonInteractive");
                startInfo.ArgumentList.Add("-Command");
                startInfo.ArgumentList.Add(command);
                break;
            case HookShell.Cmd:
                startInfo.FileName = "cmd.exe";
                startInfo.ArgumentList.Add("/d");
                startInfo.ArgumentList.Add("/s");
                startInfo.ArgumentList.Add("/c");
                startInfo.ArgumentList.Add(command);
                break;
            default:
                startInfo.FileName = "/bin/sh";
                startInfo.ArgumentList.Add("-c");
                startInfo.ArgumentList.Add(command);
                break;
        }
    }

    private static HookSpawnFailure Classify(Win32Exception exception) => exception.NativeErrorCode switch
    {
        2 or 3 => HookSpawnFailure.ExecutableNotFound,
        5 or 13 => HookSpawnFailure.AccessDenied,
        _ => HookSpawnFailure.Other,
    };

    private static HookCommandOutcome Failed(HookSpawnFailure failure, string message) =>
        new(-1, string.Empty, string.Empty, false, false, failure, message, false);

    private static HookCommandOutcome? TryStart(Process process)
    {
        try
        {
            process.Start();
            return null;
        }
        catch (Win32Exception ex)
        {
            return Failed(Classify(ex), ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Failed(HookSpawnFailure.Other, ex.Message);
        }
    }

    private static async Task<bool> WriteInputAsync(Stream stream, ReadOnlyMemory<byte> input)
    {
        try
        {
            await stream.WriteAsync(input).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
            return true;
        }
        catch (IOException)
        {
            // The hook exited or closed standard input without reading everything. That is the hook's choice.
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        finally
        {
            try
            {
                stream.Close();
            }
            catch (IOException)
            {
                // Closing a pipe whose reader is gone fails; the process is finished with it anyway.
            }
        }
    }

    private static async Task<(byte[] Bytes, bool Truncated)> CaptureAsync(Stream stream, int maxBytes)
    {
        using var kept = new MemoryStream(Math.Min(maxBytes, ReadChunkBytes));
        var chunk = new byte[ReadChunkBytes];
        var truncated = false;
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory()).ConfigureAwait(false)) > 0)
        {
            // Keep reading after the bound so a chatty hook never blocks on a full pipe; only the first bytes are kept.
            var keep = Math.Min(maxBytes - (int)kept.Length, read);
            if (keep > 0)
            {
                await kept.WriteAsync(chunk.AsMemory(0, keep)).ConfigureAwait(false);
            }

            if (keep < read)
            {
                truncated = true;
            }
        }

        return (kept.ToArray(), truncated);
    }
}
