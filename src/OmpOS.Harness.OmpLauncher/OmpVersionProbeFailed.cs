namespace OmpOS.Harness.OmpLauncher;

/// <summary>
/// The probe ran but did not produce a version.
/// </summary>
/// <param name="Failure">Why the probe failed.</param>
/// <param name="ExitCode">The exit code when the process exited; null when it never started or was killed.</param>
/// <param name="Detail">Standard error or a short explanation.</param>
public sealed record OmpVersionProbeFailed(OmpProbeFailure Failure, int? ExitCode, string Detail) : OmpVersionProbeResult;
