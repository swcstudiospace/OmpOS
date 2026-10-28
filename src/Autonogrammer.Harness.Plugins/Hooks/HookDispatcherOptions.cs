namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// Limits and policy of the hook dispatcher.
/// </summary>
public sealed record HookDispatcherOptions
{
    /// <summary>The largest JSON input a hook is sent, 8 MiB. A larger payload is not delivered and the hook counts as failed.</summary>
    public int MaxInputBytes { get; init; } = 8 * 1024 * 1024;

    /// <summary>
    /// The most standard output bytes kept and parsed, 1 MiB. Output beyond it is drained and reported as truncated; a
    /// truncated response that no longer parses is a failure of the hook (fail-open unless <c>failClosed</c>), never an
    /// invalid-response deny.
    /// </summary>
    public int MaxStandardOutputBytes { get; init; } = 1024 * 1024;

    /// <summary>The most standard error bytes kept, 64 KiB.</summary>
    public int MaxStandardErrorBytes { get; init; } = 64 * 1024;

    /// <summary>
    /// True to hand hooks the whole parent environment (Cursor-like) instead of the allow-list. This is the
    /// <c>hooks.inheritEnvironment</c> setting; it defaults to false (ADR-0013 risk 1).
    /// </summary>
    public bool InheritEnvironment { get; init; }

    /// <summary>The parent environment the child environment is built from; null reads the process environment on each run.</summary>
    public IReadOnlyDictionary<string, string>? ParentEnvironment { get; init; }
}
