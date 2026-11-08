namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>A version failed its probe after activation, so the previous good version was restored.</summary>
/// <param name="FailedVersion">The version that failed. It is quarantined.</param>
/// <param name="RestoredVersion">The version that is active again.</param>
/// <param name="Message">The explanation.</param>
public sealed record OmpRolledBack(string FailedVersion, string RestoredVersion, string Message) : OmpUpdateResult(Message);
