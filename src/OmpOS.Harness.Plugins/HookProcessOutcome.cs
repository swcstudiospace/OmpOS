namespace OmpOS.Harness.Plugins;

/// <summary>
/// The result of one hook process run.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">The kept standard output.</param>
/// <param name="StandardError">The kept standard error.</param>
/// <param name="StandardOutputTruncated">True when standard output was longer than the kept bound.</param>
/// <param name="StandardErrorTruncated">True when standard error was longer than the kept bound.</param>
public sealed record HookProcessOutcome(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool StandardOutputTruncated = false,
    bool StandardErrorTruncated = false);
