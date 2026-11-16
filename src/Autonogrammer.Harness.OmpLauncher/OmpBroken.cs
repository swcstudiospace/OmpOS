namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// The active executable does not match its recorded SHA-256. It was not run. The head offers a reinstall, and a rollback
/// when <see cref="RollbackAvailable"/> is true.
/// </summary>
/// <param name="Version">The broken active version.</param>
/// <param name="RollbackAvailable">Whether a previous good version is on disk to restore.</param>
/// <param name="Message">The explanation.</param>
public sealed record OmpBroken(string Version, bool RollbackAvailable, string Message) : OmpUpdateResult(Message);
