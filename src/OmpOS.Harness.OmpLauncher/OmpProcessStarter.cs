using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace OmpOS.Harness.OmpLauncher;

/// <summary>
/// Starts omp processes. The arguments and the working directory are explicit, the environment is an allowlist, and
/// nothing is passed through a shell. A process that cannot start is reported as <see cref="OmpLaunchFailed"/>.
/// </summary>
public static class OmpProcessStarter
{
    /// <summary>
    /// The only environment variables passed to omp. Every other variable is removed from the child environment.
    /// </summary>
    public static IReadOnlyList<string> AllowedEnvironmentVariables { get; } = ["PATH", "HOME", "LANG", "TERM"];

    /// <summary>
    /// Starts omp with the request's executable, arguments and working directory.
    /// </summary>
    /// <param name="request">What to run.</param>
    /// <returns><see cref="OmpLaunchStarted"/> with a running session, or <see cref="OmpLaunchFailed"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static OmpLaunchResult Start(OmpLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Path.IsPathRooted(request.ExecutablePath))
        {
            return new OmpLaunchFailed("The omp executable path must be absolute.");
        }

        if (!Path.IsPathRooted(request.WorkingDirectory))
        {
            return new OmpLaunchFailed("The working directory must be absolute.");
        }

        if (!Directory.Exists(request.WorkingDirectory))
        {
            return new OmpLaunchFailed($"The working directory does not exist: {request.WorkingDirectory}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment.Clear();
        foreach (var name in AllowedEnvironmentVariables)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (value is not null)
            {
                startInfo.Environment[name] = value;
            }
        }

        var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            process.Dispose();
            return new OmpLaunchFailed($"The omp executable could not be started: {exception.Message}");
        }

        return new OmpLaunchStarted(new OmpSession(process));
    }
}
