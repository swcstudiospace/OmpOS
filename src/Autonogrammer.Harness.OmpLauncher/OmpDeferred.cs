namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// The update waits for a later launch because a file is in use or cannot be moved. The active install and the state record
/// are unchanged.
/// </summary>
/// <param name="Version">The version involved.</param>
/// <param name="Message">The explanation.</param>
public sealed record OmpDeferred(string? Version, string Message) : OmpUpdateResult(Message);
