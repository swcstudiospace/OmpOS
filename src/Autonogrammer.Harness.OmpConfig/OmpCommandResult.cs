using System.Globalization;

namespace Autonogrammer.Harness.OmpConfig;

/// <summary>
/// What one omp invocation produced. A failure to start omp is reported in <see cref="StartFailure"/> with exit code -1; a
/// timeout is reported in <see cref="TimedOut"/> with exit code -1 and the output read before the kill.
/// </summary>
/// <param name="ExitCode">The exit code, or -1 when omp did not start or was killed after a timeout.</param>
/// <param name="StandardOutput">The captured standard output, empty when capture was off.</param>
/// <param name="StandardError">The captured standard error.</param>
/// <param name="TimedOut">True when the time limit passed and the process tree was killed.</param>
/// <param name="OutputTruncated">True when standard output or standard error exceeded the byte limit and the rest was dropped.</param>
public sealed record OmpCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut,
    bool OutputTruncated)
{
    /// <summary>Gets the reason omp could not be started (not found, not runnable), or null when it started.</summary>
    public string? StartFailure { get; init; }

    /// <summary>
    /// Gets a value indicating whether the invocation carried standard input. <see cref="ToString"/> then shows no output,
    /// because the invocation may be one whose output echoes a secret.
    /// </summary>
    public bool CarriedStandardInput { get; init; }

    /// <summary>Gets a value indicating whether omp started, ran to its end within the limit and exited with code 0.</summary>
    public bool Succeeded => StartFailure is null && !TimedOut && ExitCode == 0;

    /// <summary>Describes the result. Standard output and error are omitted when the invocation carried standard input.</summary>
    /// <returns>A short description.</returns>
    public override string ToString() =>
        CarriedStandardInput
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"OmpCommandResult {{ ExitCode = {ExitCode}, TimedOut = {TimedOut}, Output = [withheld] }}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"OmpCommandResult {{ ExitCode = {ExitCode}, TimedOut = {TimedOut}, OutputTruncated = {OutputTruncated}, StartFailure = {StartFailure}, StandardOutput = {StandardOutput}, StandardError = {StandardError} }}");
}
