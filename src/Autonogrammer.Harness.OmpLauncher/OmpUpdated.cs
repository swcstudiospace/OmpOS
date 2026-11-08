namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>A newer version was installed, passed its probes and is now active.</summary>
/// <param name="Version">The new active version.</param>
/// <param name="PreviousVersion">The version it replaced, or null for a first install.</param>
/// <param name="Message">The explanation.</param>
public sealed record OmpUpdated(string Version, string? PreviousVersion, string Message) : OmpUpdateResult(Message);
