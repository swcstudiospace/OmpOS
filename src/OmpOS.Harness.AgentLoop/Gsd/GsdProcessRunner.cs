using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OmpOS.Harness.AgentLoop.Gsd;

/// <summary>
/// Runs <c>node gsd-tools.cjs query ...</c> with <see cref="Process"/>. Every argument goes through <c>ArgumentList</c>; no
/// shell is involved and standard input is closed. Standard output is read up to a byte limit and standard error is drained
/// and discarded, because the tool's warnings are harmless. A timeout, a cancellation or an output overflow kills the whole
/// process tree. A failure never carries raw tool output.
/// </summary>
public sealed class GsdProcessRunner : IGsdProcessRunner
{
    private const int ReadChunkBytes = 8192;

    private readonly GsdToolsOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="GsdProcessRunner"/> class.</summary>
    /// <param name="options">The node executable, time limit and output limit.</param>
    /// <param name="timeProvider">Drives the time limit; the system clock when null.</param>
    public GsdProcessRunner(GsdToolsOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.NodeExecutable))
        {
            throw new ArgumentException("The node executable is required.", nameof(options));
        }

        if (options.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Timeout, "The time limit must be positive.");
        }

        if (options.MaxOutputBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxOutputBytes, "The output limit must be positive.");
        }

        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<string> QueryAsync(
        string gsdToolsPath,
        string queryName,
        IReadOnlyList<string> queryArguments,
        string projectDirectory,
        CancellationToken cancellationToken)
    {
        ValidateQuery(gsdToolsPath, queryName, queryArguments, projectDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        using var process = StartProcess(BuildStartInfo(gsdToolsPath, queryName, queryArguments, projectDirectory));

        using var timeout = new CancellationTokenSource(_options.Timeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        process.StandardInput.Close();
        var outputTask = ReadBoundedAsync(process.StandardOutput.BaseStream, _options.MaxOutputBytes, () => GsdProcessTree.Kill(process), linked.Token);
        var errorTask = DrainAsync(process.StandardError.BaseStream, linked.Token);

        (byte[] Bytes, bool Overflowed) output;
        var finished = false;
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);

            // The pipes close when every writer exits, so a descendant that outlives the tool is bounded by the time limit.
            output = await outputTask.ConfigureAwait(false);
            await errorTask.ConfigureAwait(false);
            finished = true;
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new GsdToolsException(GsdToolsFailure.TimedOut, "The workflow tool did not finish in time and was stopped.");
        }
        finally
        {
            if (!finished)
            {
                GsdProcessTree.Kill(process);
            }

            // Both reads end with the process tree; they are settled here so that neither outlives the process object.
            try
            {
                await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The reads were cancelled together with the run.
            }
            catch (IOException)
            {
                // The pipe closed while the process tree was being killed.
            }
            catch (ObjectDisposedException)
            {
                // The pipe closed while the process tree was being killed.
            }
        }

        return Interpret(process, output);
    }

    private static void ValidateQuery(string gsdToolsPath, string queryName, IReadOnlyList<string> queryArguments, string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gsdToolsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryName);
        ArgumentNullException.ThrowIfNull(queryArguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
    }

    private ProcessStartInfo BuildStartInfo(string gsdToolsPath, string queryName, IReadOnlyList<string> queryArguments, string projectDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.NodeExecutable,
            WorkingDirectory = projectDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(gsdToolsPath);
        startInfo.ArgumentList.Add("query");
        startInfo.ArgumentList.Add(queryName);
        foreach (var argument in queryArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add("--project-dir");
        startInfo.ArgumentList.Add(projectDirectory);
        return startInfo;
    }

    private static Process StartProcess(ProcessStartInfo startInfo)
    {
        var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
            return process;
        }
        catch (Win32Exception exception)
        {
            process.Dispose();
            throw new GsdToolsException(GsdToolsFailure.StartFailed, "The node executable could not be started.", innerException: exception);
        }
        catch (InvalidOperationException exception)
        {
            process.Dispose();
            throw new GsdToolsException(GsdToolsFailure.StartFailed, "The node executable could not be started.", innerException: exception);
        }
    }

    private static string Interpret(Process process, (byte[] Bytes, bool Overflowed) output)
    {
        if (output.Overflowed)
        {
            throw new GsdToolsException(GsdToolsFailure.OutputTooLarge, "The workflow tool wrote more output than allowed and was stopped.");
        }

        if (process.ExitCode != 0)
        {
            throw new GsdToolsException(
                GsdToolsFailure.NonZeroExit,
                string.Create(CultureInfo.InvariantCulture, $"The workflow tool exited with code {process.ExitCode}."),
                process.ExitCode);
        }

        return Encoding.UTF8.GetString(output.Bytes);
    }

    private static async Task<(byte[] Bytes, bool Overflowed)> ReadBoundedAsync(Stream stream, int maxBytes, Action stopWriter, CancellationToken cancellationToken)
    {
        using var kept = new MemoryStream();
        var chunk = new byte[ReadChunkBytes];
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (kept.Length + read > maxBytes)
            {
                // Stop the writer so it cannot block on a full pipe or keep running after the result is discarded.
                stopWriter();
                return (kept.ToArray(), true);
            }

            await kept.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return (kept.ToArray(), false);
    }

    private static async Task DrainAsync(Stream stream, CancellationToken cancellationToken)
    {
        var chunk = new byte[ReadChunkBytes];
        while (await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false) > 0)
        {
            // Warnings on standard error are harmless; they are read so that the tool never blocks on a full pipe.
        }
    }
}
