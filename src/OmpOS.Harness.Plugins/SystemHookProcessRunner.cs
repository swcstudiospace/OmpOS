using System.Diagnostics;

namespace OmpOS.Harness.Plugins;

/// <summary>
/// Runs hook commands with <see cref="Process"/>. The first argument is the executable and the rest go through
/// <c>ArgumentList</c>; no shell is involved. The working directory is set explicitly, and standard input is closed so
/// a hook cannot wait on the host's console. Output is drained as it arrives and only the first bytes are kept.
/// Cancellation kills the whole process tree, not just the direct child.
/// </summary>
public sealed class SystemHookProcessRunner : IHookProcessRunner
{
    private const int ReadChunkBytes = 4096;

    /// <inheritdoc />
    public async Task<HookProcessOutcome> RunAsync(HookCommand command, int maxOutputBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOutputBytes);

        var startInfo = new ProcessStartInfo
        {
            FileName = command.Arguments[0],
            WorkingDirectory = command.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        for (var i = 1; i < command.Arguments.Count; i++)
        {
            startInfo.ArgumentList.Add(command.Arguments[i]);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        process.StandardInput.Close();

        var standardOutput = CaptureAsync(process.StandardOutput.BaseStream, maxOutputBytes);
        var standardError = CaptureAsync(process.StandardError.BaseStream, maxOutputBytes);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            ProcessTree.Kill(process);
            throw;
        }

        // The pipes close when every writer exits. A descendant that outlives the hook and keeps a pipe open is
        // bounded by the caller's cancellation, which is what the hook runner's timeout provides.
        var (outputBytes, outputTruncated) = await standardOutput.WaitAsync(cancellationToken).ConfigureAwait(false);
        var (errorBytes, errorTruncated) = await standardError.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new HookProcessOutcome(
            process.ExitCode,
            HookOutput.Decode(outputBytes, outputTruncated),
            HookOutput.Decode(errorBytes, errorTruncated),
            outputTruncated,
            errorTruncated);
    }

    private static async Task<(byte[] Bytes, bool Truncated)> CaptureAsync(Stream stream, int maxBytes)
    {
        using var kept = new MemoryStream(Math.Min(maxBytes, ReadChunkBytes));
        var chunk = new byte[ReadChunkBytes];
        var truncated = false;
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory()).ConfigureAwait(false)) > 0)
        {
            // Keep reading after the bound so that a chatty hook never blocks on a full pipe; only the first bytes are kept.
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
