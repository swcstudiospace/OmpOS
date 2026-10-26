namespace OmpOS.Harness.Plugins;

/// <summary>
/// What happened to one hook in a run.
/// </summary>
public enum HookExecutionStatus
{
    /// <summary>The hook exited with code 0.</summary>
    Succeeded,

    /// <summary>The hook exited with a non-zero code that is not a denial. The action proceeds.</summary>
    Failed,

    /// <summary>A blocking hook exited with code 2. The action is denied.</summary>
    Denied,

    /// <summary>The hook ran past its timeout. Its process tree was killed.</summary>
    TimedOut,

    /// <summary>
    /// The hook did not start because consent is not recorded for its exact command, trigger and blocking flag.
    /// A blocking hook in this state denies the action.
    /// </summary>
    AwaitingConsent,

    /// <summary>
    /// The blocking hook did not start because its plugin is quarantined. The action is denied with
    /// <see cref="HookDenialReason.PluginQuarantined"/>.
    /// </summary>
    PluginQuarantined,

    /// <summary>
    /// The hook did not start. An earlier blocking hook stopped the run, or the hook is non-blocking and its plugin is
    /// quarantined.
    /// </summary>
    Skipped,

    /// <summary>The runner threw. The plugin that owns the hook has been quarantined.</summary>
    Faulted,
}
