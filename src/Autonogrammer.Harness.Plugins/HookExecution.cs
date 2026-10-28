namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// The record of one hook in one run. Output is the bounded text the process wrote, and the flags say whether it was cut.
/// </summary>
/// <param name="HookId">The hook identifier.</param>
/// <param name="PluginId">The plugin that declared the hook.</param>
/// <param name="Status">What happened.</param>
/// <param name="CommandHash">The consent identity of the command that was, or would have been, run.</param>
/// <param name="ExitCode">The exit code, when the process exited.</param>
/// <param name="Duration">How long the hook ran; zero when it did not start.</param>
/// <param name="StandardOutput">The kept standard output.</param>
/// <param name="StandardError">The kept standard error.</param>
/// <param name="StandardOutputTruncated">True when standard output was longer than <see cref="HookOutput.MaxBytes"/>.</param>
/// <param name="StandardErrorTruncated">True when standard error was longer than <see cref="HookOutput.MaxBytes"/>.</param>
public sealed record HookExecution(
    string HookId,
    string PluginId,
    HookExecutionStatus Status,
    string CommandHash,
    int? ExitCode,
    TimeSpan Duration,
    string StandardOutput,
    string StandardError,
    bool StandardOutputTruncated,
    bool StandardErrorTruncated);
