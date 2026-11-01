namespace Autonogrammer.Harness.Isolation;

/// <summary>The result of one <see cref="IIsolationProcessRunner"/> call.</summary>
/// <param name="ExitCode">The process exit code, or -1 when it did not exit.</param>
/// <param name="Output">The combined standard output and standard error. It may be empty.</param>
/// <param name="TimedOut">True when the wait exceeded the timeout.</param>
/// <param name="ExecutableMissing">True when the executable could not be found.</param>
public sealed record IsolationProcessRun(int ExitCode, string Output, bool TimedOut, bool ExecutableMissing);
