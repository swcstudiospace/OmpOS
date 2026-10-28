namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// Limits for hook runs.
/// </summary>
public sealed record HookRunnerOptions
{
    /// <summary>
    /// The longest a hook may run before its whole process tree is killed. The default is 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}
