namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// The outcome of a managed omp check, install or recovery. Consumers match on the derived results; every result carries a
/// <see cref="Reason"/> the head can show.
/// </summary>
/// <param name="Reason">The user-readable explanation.</param>
public abstract record OmpUpdateResult(string Reason);
