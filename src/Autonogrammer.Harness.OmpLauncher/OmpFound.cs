namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// omp was found and its file is runnable.
/// </summary>
/// <param name="ExecutablePath">The path of the executable, as it was checked.</param>
public sealed record OmpFound(string ExecutablePath) : OmpLocation;
