namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>A version that failed its health check and is not installed again.</summary>
/// <param name="Version">The quarantined version.</param>
/// <param name="Reason">The failure, in user-readable form.</param>
/// <param name="QuarantinedAt">When the version was quarantined.</param>
public sealed record OmpQuarantinedVersion(string Version, string Reason, DateTimeOffset QuarantinedAt);
