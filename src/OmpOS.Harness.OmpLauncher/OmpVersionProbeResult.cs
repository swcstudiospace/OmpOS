namespace OmpOS.Harness.OmpLauncher;

/// <summary>
/// The result of <see cref="OmpVersionProbe.ProbeAsync"/>. Consumers match on <see cref="OmpVersionReported"/> or
/// <see cref="OmpVersionProbeFailed"/>. A failed probe is distinct from an omp that was not found.
/// </summary>
public abstract record OmpVersionProbeResult;
