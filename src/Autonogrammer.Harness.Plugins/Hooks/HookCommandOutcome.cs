namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// The result of one hook process.
/// </summary>
/// <param name="ExitCode">The exit code; -1 when the process did not start.</param>
/// <param name="StandardOutput">The kept standard output.</param>
/// <param name="StandardError">The kept standard error.</param>
/// <param name="StandardOutputTruncated">True when standard output was longer than the kept bound. The kept part is still complete up to the cut.</param>
/// <param name="StandardErrorTruncated">True when standard error was longer than the kept bound.</param>
/// <param name="SpawnFailure">Why the process did not start, or <see cref="HookSpawnFailure.None"/>.</param>
/// <param name="SpawnMessage">The operating system's explanation of a start failure.</param>
/// <param name="StandardInputDelivered">False when the hook closed its standard input before the whole payload was written.</param>
public sealed record HookCommandOutcome(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool StandardOutputTruncated,
    bool StandardErrorTruncated,
    HookSpawnFailure SpawnFailure = HookSpawnFailure.None,
    string? SpawnMessage = null,
    bool StandardInputDelivered = true);
