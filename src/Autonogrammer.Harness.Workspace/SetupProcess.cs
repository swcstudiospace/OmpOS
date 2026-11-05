using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Autonogrammer.Workspace;

/// <summary>
/// Runs one setup step with an argument list, an explicit working directory and an explicit environment.
/// No shell is involved. Output is drained and bounded; only a short failure detail is kept.
/// </summary>
internal static class SetupProcess
{
    private const int MaxCapturedCharacters = 4096;
    private const int MaxDetailCharacters = 512;

    /// <summary>
    /// Runs a step and waits for it to exit.
    /// </summary>
    /// <param name="executable">The executable path or name.</param>
    /// <param name="arguments">The argument list.</param>
    /// <param name="workingDirectory">The existing working directory.</param>
    /// <param name="environment">Variables added to the inherited environment.</param>
    /// <param name="cancellationToken">Cancels the wait. The process tree is killed.</param>
    public static Task<StepExecution> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken) =>
        RunAsync(executable, arguments, workingDirectory, environment, KillEntireTree, cancellationToken);

    /// <summary>
    /// Runs a step with an explicit tree-kill action. Production code uses the public overload; the seam lets a test
    /// make the OS refuse the kill.
    /// </summary>
    /// <param name="executable">The executable path or name.</param>
    /// <param name="arguments">The argument list.</param>
    /// <param name="workingDirectory">The existing working directory.</param>
    /// <param name="environment">Variables added to the inherited environment.</param>
    /// <param name="killTree">Stops the process tree. It may throw; a <see cref="Win32Exception"/> is reported as a failed step.</param>
    /// <param name="cancellationToken">Cancels the wait. The process tree is killed through <paramref name="killTree"/>.</param>
    internal static async Task<StepExecution> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        Action<Process> killTree,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = BuildStartInfo(executable, arguments, workingDirectory, environment) };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            return StepExecution.Failed($"could not start '{executable}': {exception.Message}");
        }

        process.StandardInput.Close();
        var standardOutput = DrainAsync(process.StandardOutput);
        var standardError = DrainAsync(process.StandardError);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var killFailure = KillProcessTree(process, killTree);
            await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
            if (killFailure is not null)
            {
                return StepExecution.Failed($"could not stop '{executable}' after cancellation: {killFailure.Message}");
            }
            throw;
        }

        await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
        if (process.ExitCode == 0)
        {
            return StepExecution.Success;
        }

        var detail = FirstLine(await standardError.ConfigureAwait(false));
        return StepExecution.Failed(detail.Length == 0
            ? $"exited with code {process.ExitCode}"
            : $"exited with code {process.ExitCode}: {detail}");
    }

    private static ProcessStartInfo BuildStartInfo(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        return startInfo;
    }

    private static async Task<string> DrainAsync(StreamReader reader)
    {
        var captured = new StringBuilder();
        var buffer = new char[1024];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
        {
            if (captured.Length < MaxCapturedCharacters)
            {
                captured.Append(buffer, 0, Math.Min(read, MaxCapturedCharacters - captured.Length));
            }
        }

        return captured.ToString();
    }

    private static string FirstLine(string text)
    {
        var line = text.Split('\n')
            .Select(candidate => candidate.Trim())
            .FirstOrDefault(candidate => candidate.Length > 0) ?? string.Empty;
        return line.Length <= MaxDetailCharacters ? line : line[..MaxDetailCharacters];
    }

    private static void KillEntireTree(Process process) => process.Kill(entireProcessTree: true);

    /// <summary>
    /// Stops a process tree. A process that has already exited is not reported. A kill the OS refuses is returned
    /// so the caller can report the step as failed.
    /// </summary>
    /// <param name="process">The process to stop.</param>
    /// <param name="killTree">The action that performs the kill.</param>
    /// <returns>The refusal, or null when the process is gone or was stopped.</returns>
    internal static Win32Exception? KillProcessTree(Process process, Action<Process> killTree)
    {
        if (process.HasExited)
        {
            return null;
        }

        try
        {
            killTree(process);
        }
        catch (InvalidOperationException)
        {
            // The process exited between the check and the kill; nothing is left to stop.
        }
        catch (Win32Exception exception)
        {
            // The OS refused the signal, so the process may still be running. The caller reports it.
            return exception;
        }

        return null;
    }

    /// <summary>
    /// The outcome of one step.
    /// </summary>
    /// <param name="Succeeded">Whether the step exited with status zero.</param>
    /// <param name="Failure">A short description of the failure, or null when the step succeeded.</param>
    internal sealed record StepExecution(bool Succeeded, string? Failure)
    {
        /// <summary>Gets the outcome of a step that exited with status zero.</summary>
        public static StepExecution Success { get; } = new(true, null);

        /// <summary>
        /// Creates the outcome of a failed step.
        /// </summary>
        /// <param name="failure">A short description of the failure.</param>
        public static StepExecution Failed(string failure) => new(false, failure);
    }
}
