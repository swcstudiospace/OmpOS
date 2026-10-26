namespace OmpOS.Harness.OmpLauncher;

/// <summary>
/// The probe ran and printed a version.
/// </summary>
/// <param name="Version">The trimmed standard output of the probe.</param>
public sealed record OmpVersionReported(string Version) : OmpVersionProbeResult;
