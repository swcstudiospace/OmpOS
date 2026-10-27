namespace OmpOS.Harness.Remote;

/// <summary>What a short ssh child process did.</summary>
/// <param name="ExitCode">The exit code, or -1 when the process did not start or was stopped for taking too long.</param>
/// <param name="StandardOutput">Everything the process wrote to stdout, cut at 1 MiB.</param>
/// <param name="StandardError">Everything the process wrote to stderr, cut at 1 MiB.</param>
/// <param name="TimedOut">Whether the process was killed because it ran past its time limit.</param>
/// <param name="StartFailure">Why the process could not be started (the executable is missing or not runnable), or <see langword="null"/>.</param>
internal sealed record SshProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut, string? StartFailure);
