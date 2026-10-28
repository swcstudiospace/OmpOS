using System.Diagnostics;

namespace OmpOS.Native.Tests.Composition.Mentions;

/// <summary>Runs the real <c>git</c> to build the repositories of the git mention tests.</summary>
internal static class GitCli
{
    /// <summary>Gets whether a <c>git</c> executable is on the PATH. The tests that need one skip themselves when it is not.</summary>
    internal static bool IsOnPath { get; } = FindOnPath();

    /// <summary>Runs git in a folder and fails the test when git exits with an error.</summary>
    /// <param name="workingDirectory">The folder git runs in.</param>
    /// <param name="arguments">The git arguments.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when git has exited.</returns>
    internal static async Task RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("git did not start.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(true);
        await output.ConfigureAwait(true);
        var message = await error.ConfigureAwait(true);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("git " + string.Join(' ', arguments) + " failed: " + message);
        }
    }

    /// <summary>Creates a repository whose first branch is named <paramref name="branch"/> and has no commit yet.</summary>
    /// <param name="directory">The folder to turn into a repository.</param>
    /// <param name="branch">The name of the first branch.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the repository exists.</returns>
    internal static async Task InitAsync(string directory, string branch, CancellationToken cancellationToken)
    {
        await RunAsync(directory, ["init", "--quiet"], cancellationToken).ConfigureAwait(true);
        await RunAsync(directory, ["symbolic-ref", "HEAD", "refs/heads/" + branch], cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Stages every change and commits it with a fixed identity and no signature.</summary>
    /// <param name="directory">The repository.</param>
    /// <param name="message">The commit message.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the commit exists.</returns>
    internal static async Task CommitAllAsync(string directory, string message, CancellationToken cancellationToken)
    {
        await RunAsync(directory, ["add", "-A"], cancellationToken).ConfigureAwait(true);
        await RunAsync(
            directory,
            ["-c", "user.name=Test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", message],
            cancellationToken).ConfigureAwait(true);
    }

    private static bool FindOnPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (File.Exists(Path.Combine(directory, "git")) || File.Exists(Path.Combine(directory, "git.exe")))
            {
                return true;
            }
        }

        return false;
    }
}
