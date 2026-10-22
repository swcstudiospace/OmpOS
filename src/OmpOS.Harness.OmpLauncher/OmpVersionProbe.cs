using System.Text;

namespace OmpOS.Harness.OmpLauncher;

/// <summary>
/// Runs <c>omp version</c> to read the installed version. The probe runs the executable with exactly one argument,
/// <c>version</c>, under a timeout. When the timeout passes, the process tree is killed and the probe reports
/// <see cref="OmpProbeFailure.TimedOut"/>.
/// </summary>
public static class OmpVersionProbe
{
    /// <summary>The argument that asks omp for its version.</summary>
    public const string VersionArgument = "version";

    private const int MaxOutputCharacters = 4096;

    /// <summary>The time allowed for the probe before its process tree is killed.</summary>
    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Probes the version of the executable at <paramref name="executablePath"/>.
    /// </summary>
    /// <param name="executablePath">The absolute path of the executable to probe.</param>
    /// <param name="cancellationToken">Cancels the probe. A cancelled probe throws; it is not reported as a failure.</param>
    /// <param name="timeout">The time allowed for the probe. Null uses <see cref="DefaultTimeout"/>.</param>
    /// <returns>The reported version, or the reason the probe failed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is zero or negative.</exception>
    public static async Task<OmpVersionProbeResult> ProbeAsync(
        string executablePath,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultTimeout;
        if (limit <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "The probe timeout must be positive.");
        }

        var started = OmpProcessStarter.Start(
            new OmpLaunchRequest(executablePath, [VersionArgument], Path.GetTempPath()));
        if (started is not OmpLaunchStarted launched)
        {
            var failed = (OmpLaunchFailed)started;
            return new OmpVersionProbeFailed(OmpProbeFailure.StartFailed, null, failed.Reason);
        }

        using var session = launched.Session;
        using var limitSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limitSource.CancelAfter(limit);
        var token = limitSource.Token;

        var standardOutput = BoundedText.ReadAsync(session.StandardOutput, MaxOutputCharacters);
        var standardError = BoundedText.ReadAsync(session.StandardError, MaxOutputCharacters);
        try
        {
            var exitCode = await session.WaitForExitAsync(token).ConfigureAwait(false);
            var output = (await standardOutput.WaitAsync(token).ConfigureAwait(false)).Trim();
            var error = (await standardError.WaitAsync(token).ConfigureAwait(false)).Trim();

            if (exitCode != 0)
            {
                return new OmpVersionProbeFailed(
                    OmpProbeFailure.NonZeroExit,
                    exitCode,
                    error.Length > 0 ? error : $"omp {VersionArgument} exited with code {exitCode}.");
            }

            if (output.Length == 0)
            {
                return new OmpVersionProbeFailed(
                    OmpProbeFailure.EmptyOutput,
                    exitCode,
                    $"omp {VersionArgument} printed nothing.");
            }

            return new OmpVersionReported(output);
        }
        catch (OperationCanceledException) when (limitSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            session.Kill();
            await AwaitReadsAfterKillAsync(standardOutput, standardError).ConfigureAwait(false);
            return new OmpVersionProbeFailed(
                OmpProbeFailure.TimedOut,
                null,
                $"omp {VersionArgument} did not exit within {limit.TotalSeconds:0.###} seconds and was killed.");
        }
    }

    // After a kill the pipes close, so both reads finish quickly. A grandchild that escaped the tree could keep a pipe
    // open, so the wait is bounded and its failure is ignored: the probe has already timed out.
    private static async Task AwaitReadsAfterKillAsync(Task<string> standardOutput, Task<string> standardError)
    {
        try
        {
            await Task.WhenAll(standardOutput, standardError).WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or TimeoutException or IOException or ObjectDisposedException)
        {
            // Nothing more can be read from a killed process. The timeout result stands.
        }
    }

    private static class BoundedText
    {
        /// <summary>Reads the stream to its end, keeping at most <paramref name="maxCharacters"/> characters.</summary>
        public static async Task<string> ReadAsync(TextReader reader, int maxCharacters)
        {
            var builder = new StringBuilder();
            var buffer = new char[1024];
            int read;
            while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                var room = maxCharacters - builder.Length;
                if (room > 0)
                {
                    builder.Append(buffer, 0, Math.Min(room, read));
                }
            }

            return builder.ToString();
        }
    }
}
