using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace OmpOS.Worktrees;

/// <summary>
/// Runs git with an argument list and an explicit working directory. No shell is involved.
/// </summary>
internal static class GitProcess
{
    /// <summary>
    /// Environment variables that would redirect git to a repository other than the one named by the working directory.
    /// </summary>
    private static readonly string[] RepositoryRedirectVariables =
    [
        "GIT_DIR",
        "GIT_WORK_TREE",
        "GIT_INDEX_FILE",
        "GIT_COMMON_DIR",
        "GIT_NAMESPACE",
    ];

    /// <summary>
    /// Runs git and returns its exit code and output without throwing on a non-zero exit.
    /// </summary>
    /// <param name="git">The git executable path or name, resolved via PATH when it has no directory part.</param>
    /// <param name="workingDirectory">The directory git runs in.</param>
    /// <param name="arguments">The git arguments, passed as an argument list.</param>
    /// <param name="cancellationToken">Cancels the wait; the git process is killed.</param>
    /// <returns>The exit code, standard output and standard error.</returns>
    public static async Task<GitResult> RunAsync(
        string git,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = git,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var variable in RepositoryRedirectVariables)
        {
            startInfo.Environment.Remove(variable);
        }

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new WorktreeGitException(
                $"Could not start git executable '{git}' in '{workingDirectory}'.",
                exception,
                exitCode: null);
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillQuietly(process);
            throw;
        }

        return new GitResult(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
    }

    /// <summary>
    /// Runs git and throws <see cref="WorktreeGitException"/> when it exits with a non-zero status.
    /// </summary>
    /// <param name="git">The git executable path or name.</param>
    /// <param name="workingDirectory">The directory git runs in.</param>
    /// <param name="operation">A description of the operation, used in the exception message.</param>
    /// <param name="arguments">The git arguments.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The standard output text.</returns>
    public static async Task<string> RunOrThrowAsync(
        string git,
        string workingDirectory,
        string operation,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(git, workingDirectory, arguments, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new WorktreeGitException(
                $"git {operation} failed with exit code {result.ExitCode}: {result.StandardError.Trim()}",
                result.ExitCode,
                result.StandardError);
        }

        return result.StandardOutput;
    }

    private static void KillQuietly(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process exited before the kill; nothing left to stop.
        }
    }
}
