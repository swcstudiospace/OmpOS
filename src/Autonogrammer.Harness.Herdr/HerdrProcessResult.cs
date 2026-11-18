namespace Autonogrammer.Harness.Herdr;

/// <summary>The result of one herdr command-line invocation.</summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">Everything the process wrote to standard output.</param>
/// <param name="StandardError">Everything the process wrote to standard error.</param>
public sealed record HerdrProcessResult(int ExitCode, string StandardOutput, string StandardError);
