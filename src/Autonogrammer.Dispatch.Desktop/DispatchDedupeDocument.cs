namespace Autonogrammer.Dispatch.Desktop;

/// <summary>The persisted dedupe window: the highest applied sequence and the newest command identifiers with their nonces.</summary>
/// <param name="Version">The format version. Only 1 is read.</param>
/// <param name="HighWater">The highest sequence applied.</param>
/// <param name="Recent">The newest applied commands, oldest first, at most 1024.</param>
public sealed record DispatchDedupeDocument(int Version, long HighWater, IReadOnlyList<DispatchRecentCommand> Recent);
