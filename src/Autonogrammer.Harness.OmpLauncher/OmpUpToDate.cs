namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>The active version is the latest published version, or newer. Nothing was installed.</summary>
/// <param name="Version">The active version.</param>
/// <param name="CheckedAt">When the registry was read.</param>
/// <param name="Message">The explanation.</param>
public sealed record OmpUpToDate(string Version, DateTimeOffset CheckedAt, string Message) : OmpUpdateResult(Message);
