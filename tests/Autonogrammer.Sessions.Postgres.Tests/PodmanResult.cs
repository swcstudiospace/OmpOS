namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>The outcome of one podman invocation.</summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">Everything the process wrote to standard output.</param>
/// <param name="StandardError">Everything the process wrote to standard error.</param>
internal sealed record PodmanResult(int ExitCode, string StandardOutput, string StandardError);
