using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OmpOS.Worktrees;

/// <summary>
/// Runs setup commands the way Cursor documents them: each command through the system shell in the new worktree, with <c>ROOT_WORKTREE_PATH</c> set to the main checkout, one after the other,
/// stopping at the first failure. A script entry is run as one command through the same shell. Only <see cref="ConsentedWorktreeSetupRunner"/> calls it, after consent.
/// </summary>
/// <remarks>The shell is <c>/bin/sh -c</c> on macOS and Linux and <c>powershell.exe -NoProfile -Command</c> on Windows (UNVERIFIED on Windows: run a worktrees.json with <c>setup-worktree-windows</c> there).</remarks>
public sealed class ShellWorktreeSetupExecutor : IWorktreeSetupExecutor
{
    private const int MaxDetailChars = 400;

    private readonly bool _windows;

    /// <summary>Creates the executor.</summary>
    /// <param name="windows">Whether to use PowerShell instead of <c>/bin/sh</c>; the operating system when <see langword="null"/>.</param>
    public ShellWorktreeSetupExecutor(bool? windows = null) => _windows = windows ?? OperatingSystem.IsWindows();

    /// <inheritdoc />
    public async Task<WorktreeSetupResult> ExecuteAsync(WorktreeSetupExecution execution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execution);

        var commands = execution.Setup.ScriptPath is { } script ? [Quote(script)] : execution.Setup.Commands;
        var run = 0;
        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            run++;
            var failure = await RunOneAsync(command, execution, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return new WorktreeSetupResult(WorktreeSetupStatus.Failed, run, string.Create(CultureInfo.InvariantCulture, $"Setup command {run} of {commands.Count} failed: {failure}"));
            }
        }

        return new WorktreeSetupResult(WorktreeSetupStatus.Completed, run, string.Create(CultureInfo.InvariantCulture, $"Ran {run} setup {(run == 1 ? "command" : "commands")}."));
    }

    private string Quote(string path) => _windows ? "& '" + path.Replace("'", "''", StringComparison.Ordinal) + "'" : "sh '" + path.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private async Task<string?> RunOneAsync(string command, WorktreeSetupExecution execution, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _windows ? "powershell.exe" : "/bin/sh",
            WorkingDirectory = execution.WorktreePath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (_windows)
        {
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
        }
        else
        {
            startInfo.ArgumentList.Add("-c");
        }

        startInfo.ArgumentList.Add(command);
        startInfo.Environment[CursorWorktreesConfig.RootWorktreePathVariable] = execution.ProjectRoot;

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            return "could not start the shell: " + exception.Message;
        }

        process.StandardInput.Close();
        var output = DrainAsync(process.StandardOutput);
        var error = DrainAsync(process.StandardError);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            await Task.WhenAll(output, error).ConfigureAwait(false);
            throw;
        }

        await Task.WhenAll(output, error).ConfigureAwait(false);
        if (process.ExitCode == 0)
        {
            return null;
        }

        var detail = (await error.ConfigureAwait(false)).Trim();
        return detail.Length == 0
            ? string.Create(CultureInfo.InvariantCulture, $"exited with code {process.ExitCode}")
            : string.Create(CultureInfo.InvariantCulture, $"exited with code {process.ExitCode}: {(detail.Length <= MaxDetailChars ? detail : detail[..MaxDetailChars])}");
    }

    private static async Task<string> DrainAsync(StreamReader reader)
    {
        var captured = new StringBuilder();
        var buffer = new char[1024];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
        {
            if (captured.Length < MaxDetailChars)
            {
                captured.Append(buffer, 0, Math.Min(read, MaxDetailChars - captured.Length));
            }
        }

        return captured.ToString();
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process exited before the kill; nothing is left to stop.
        }
    }
}
