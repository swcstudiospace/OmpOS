namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// The outcome of one <see cref="IOmpProcessRunner.RunAsync"/> call.
/// </summary>
/// <param name="Outcome">How the run ended.</param>
/// <param name="ExitCode">The exit code when the process exited; null otherwise.</param>
/// <param name="Output">The tail of standard output followed by standard error. Empty when the run did not exit.</param>
/// <param name="StartError">The exception that stopped the process from starting; null otherwise.</param>
public sealed record OmpProcessResult(OmpProcessOutcome Outcome, int? ExitCode, string Output, Exception? StartError);
