namespace Autonogrammer.Worktrees;

/// <summary>
/// The outcome of one git invocation.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">Everything git wrote to standard output.</param>
/// <param name="StandardError">Everything git wrote to standard error.</param>
internal sealed record GitResult(int ExitCode, string StandardOutput, string StandardError);
