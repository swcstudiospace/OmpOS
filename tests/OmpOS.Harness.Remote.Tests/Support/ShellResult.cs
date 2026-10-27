namespace OmpOS.Harness.Remote.Tests;

/// <summary>What a local program did.</summary>
/// <param name="ExitCode">The exit code.</param>
/// <param name="StandardOutput">What it wrote to stdout.</param>
/// <param name="StandardError">What it wrote to stderr.</param>
internal sealed record ShellResult(int ExitCode, string StandardOutput, string StandardError);
