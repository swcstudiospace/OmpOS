namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// What happened to one hook in one dispatch.
/// </summary>
public enum HookRunStatus
{
    /// <summary>The hook ran and its answer was used (or it had nothing to say).</summary>
    Succeeded,

    /// <summary>The hook denied the action: it answered <c>deny</c>, or exited with code 2.</summary>
    Denied,

    /// <summary>The hook crashed, exited with another non-zero code, could not start or produced no usable output.</summary>
    Failed,

    /// <summary>The hook outlived its timeout and its process tree was killed.</summary>
    TimedOut,

    /// <summary>The hook did not start: disabled elsewhere, its matcher is invalid, or it is not allowed to run here.</summary>
    Skipped,

    /// <summary>The runner itself failed, so the plugin or pseudo plugin that owns the hook was quarantined.</summary>
    Faulted,

    /// <summary>The hook did not start because consent is not recorded for its exact identity.</summary>
    AwaitingConsent,

    /// <summary>The hook did not start because its plugin or pseudo plugin is quarantined.</summary>
    PluginQuarantined,

    /// <summary>The hook exited 0 but its output was not valid JSON or did not match the event's schema.</summary>
    InvalidResponse,
}
