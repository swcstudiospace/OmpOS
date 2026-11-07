namespace Autonogrammer.Cowork.Artifacts;

/// <summary>Account-wide counters used for quota checks.</summary>
/// <param name="ArtifactCount">How many artifacts are stored.</param>
/// <param name="TotalBytes">Sum of stored version bytes.</param>
public sealed record ArtifactUsage(int ArtifactCount, long TotalBytes);
