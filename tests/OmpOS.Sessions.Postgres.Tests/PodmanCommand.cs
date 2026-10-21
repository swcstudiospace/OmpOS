using System.ComponentModel;
using System.Diagnostics;

namespace OmpOS.Sessions.Postgres.Tests;

/// <summary>Runs the podman CLI without a shell.</summary>
internal static class PodmanCommand
{
    /// <summary>Runs <c>podman</c> with the given arguments.</summary>
    /// <param name="arguments">The arguments, passed as an argument vector (never through a shell).</param>
    /// <param name="environment">Extra environment variables for the child process, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the wait for exit.</param>
    /// <returns>The exit code and captured output.</returns>
    /// <exception cref="InvalidOperationException">The podman executable cannot be started.</exception>
    internal static async Task<PodmanResult> RunAsync(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string?>? environment, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("podman")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                info.Environment[key] = value;
            }
        }

        using var process = StartProcess(info);
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new PodmanResult(process.ExitCode, await standardOutput.ConfigureAwait(false), await standardError.ConfigureAwait(false));
    }

    private static Process StartProcess(ProcessStartInfo info)
    {
        try
        {
            return Process.Start(info) ?? throw new InvalidOperationException("podman did not start.");
        }
        catch (Win32Exception failure)
        {
            throw new InvalidOperationException(
                "No PostgreSQL is available for the session store tests: set OMPOS_TEST_POSTGRES_CONNECTION to an administrative " +
                "connection string, or install podman so the test can start a PostgreSQL 17 container.",
                failure);
        }
    }
}
