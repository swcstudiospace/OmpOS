namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// Why a blocking hook denied an action. The reasons are kept apart, so a caller can tell a hook's own refusal from a
/// hook that never ran because the user has not yet consented to its exact command, or because its plugin is quarantined.
/// </summary>
public enum HookDenialReason
{
    /// <summary>A blocking hook exited with code 2.</summary>
    ExitCodeTwo,

    /// <summary>A blocking hook has no recorded consent for its exact command, so it did not start.</summary>
    ConsentMissing,

    /// <summary>A blocking hook belongs to a quarantined plugin, so it did not start.</summary>
    PluginQuarantined,
}
