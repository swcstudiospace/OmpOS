namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// omp was not found. Every path that was checked is listed, in the order checked.
/// </summary>
/// <param name="TriedPaths">Each candidate path that was checked, in search order, without duplicates.</param>
/// <param name="InstallGuidance">Text that tells the operator how to make omp available.</param>
public sealed record OmpNotFound(IReadOnlyList<string> TriedPaths, string InstallGuidance) : OmpLocation;
