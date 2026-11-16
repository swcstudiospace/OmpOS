namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>A managed omp operation failed. The kind says which failure mode it is.</summary>
/// <param name="Kind">Why it failed.</param>
/// <param name="Version">The version involved, or null.</param>
/// <param name="Message">The explanation.</param>
public sealed record OmpUpdateFailed(OmpFailureKind Kind, string? Version, string Message) : OmpUpdateResult(Message);
