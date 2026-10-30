namespace Autonogrammer.Harness.Terminal;

/// <summary>
/// The result of one process run.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">The captured standard output.</param>
/// <param name="StandardError">The captured standard error.</param>
/// <param name="StandardOutputTruncated">True when the runner dropped standard output beyond the bound.</param>
/// <param name="StandardErrorTruncated">True when the runner dropped standard error beyond the bound.</param>
public sealed record ProcessOutcome(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool StandardOutputTruncated = false,
    bool StandardErrorTruncated = false);
