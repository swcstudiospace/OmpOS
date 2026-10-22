using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Xunit;

namespace OmpOS.Worktrees.Tests;

/// <summary>
/// Runs git for test fixtures and for asserting repository state. A missing git executable throws, so the tests fail rather than skip.
/// </summary>
internal static class GitCli
{
    /// <summary>
    /// Runs git in a directory and returns its standard output.
    /// </summary>
    /// <param name="workingDirectory">The directory git runs in.</param>
    /// <param name="arguments">The git arguments.</param>
    /// <returns>The standard output text.</returns>
    /// <exception cref="InvalidOperationException">git is not on PATH, or it exited with a non-zero status.</exception>
    public static async Task<string> RunAsync(string workingDirectory, params string[] arguments)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
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

        startInfo.Environment.Remove("GIT_DIR");
        startInfo.Environment.Remove("GIT_WORK_TREE");
        startInfo.Environment.Remove("GIT_INDEX_FILE");
        startInfo.Environment.Remove("GIT_COMMON_DIR");

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                "git was not found on PATH. These tests require git and fail, rather than skip, when it is missing.",
                exception);
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(true);
        var output = await standardOutput.ConfigureAwait(true);
        var error = await standardError.ConfigureAwait(true);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} failed in '{workingDirectory}' with exit code {process.ExitCode}: {error.Trim()}");
        }

        return output;
    }
}
