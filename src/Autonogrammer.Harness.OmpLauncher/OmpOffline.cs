namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>The registry could not be reached. The active version is kept and shown with when it was last checked.</summary>
/// <param name="StaleVersion">The active version that keeps running, or null when no managed install exists.</param>
/// <param name="LastCheckedAt">When the registry was last read successfully, or null when never.</param>
/// <param name="Message">The explanation.</param>
public sealed record OmpOffline(string? StaleVersion, DateTimeOffset? LastCheckedAt, string Message) : OmpUpdateResult(Message);
