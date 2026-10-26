namespace OmpOS.Harness.OmpLauncher;

/// <summary>
/// The outcome of looking for the omp executable. Consumers match on <see cref="OmpFound"/> or <see cref="OmpNotFound"/>.
/// </summary>
public abstract record OmpLocation;
