namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// Why a dispatch denied an action. The causes are kept apart so the page can tell a hook's own refusal from a guard that
/// never ran.
/// </summary>
public enum HookDecisionCause
{
    /// <summary>The dispatch did not deny.</summary>
    None,

    /// <summary>A hook answered <c>deny</c>.</summary>
    HookDenied,

    /// <summary>A hook exited with code 2.</summary>
    ExitCodeTwo,

    /// <summary>A guarding hook has no recorded consent for its exact identity, so it did not start.</summary>
    ConsentMissing,

    /// <summary>A guarding hook belongs to a quarantined plugin or source, so it did not start.</summary>
    PluginQuarantined,

    /// <summary>A permission hook exited 0 with invalid JSON or a response that does not match the event's schema.</summary>
    InvalidResponse,

    /// <summary>A <c>failClosed</c> hook crashed, exited non-zero, could not start or produced no output.</summary>
    FailClosedFailure,

    /// <summary>A <c>failClosed</c> hook outlived its timeout.</summary>
    FailClosedTimeout,

    /// <summary>A <c>failClosed</c> hook has a matcher that is not a valid regular expression.</summary>
    InvalidMatcher,

    /// <summary>The guarding hook's runner failed, so its source was quarantined.</summary>
    RunnerFault,
}
