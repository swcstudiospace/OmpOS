namespace Autonogrammer.Presentation.Hooks;

/// <summary>Why a hook cannot run right now. The runner checks quarantine before consent, so quarantine wins when both apply.</summary>
public enum HookBlockReason
{
    /// <summary>Nothing blocks the hook.</summary>
    None,

    /// <summary>Consent is not recorded for the hook's exact command.</summary>
    AwaitingConsent,

    /// <summary>The plugin that declared the hook is quarantined.</summary>
    PluginQuarantined,
}
